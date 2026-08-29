using MEC;

using Newtonsoft.Json;

using NiveraAPI.Discord;

using UnityEngine;
using UnityEngine.Networking;

namespace PRTS.Discord;

/// <summary>
/// Represents a client for sending and receiving Discord webhooks.
/// </summary>
public class UnityWebhookClient : WebhookClient
{
    /// <summary>
    /// Represents a Discord 429 error response.
    /// </summary>
    public class Discord429
    {
        /// <summary>
        /// The time in seconds until the rate limit resets.
        /// </summary>
        public float retry_after;
    }
    
    /// <summary>
    /// Represents a bucket of requests.
    /// </summary>
    public class DiscordBucket
    {
        /// <summary>
        /// The remaining requests in the bucket.
        /// </summary>
        public int Remaining = 5;
        
        /// <summary>
        /// The time at which the bucket will be reset.
        /// </summary>
        public float ResetAt;
    }
    
    private CoroutineHandle updateCoroutine;
    
    private float globalResetAt;
    private bool globalLimited;
    
    private Dictionary<string, DiscordBucket> buckets = new();
    private Dictionary<string, string> urlHashes = new();
    
    /// <summary>
    /// Creates a new instance of the <see cref="UnityWebhookClient"/> class.
    /// </summary>
    public UnityWebhookClient(string webhookUrl) : base(webhookUrl)
    {
        updateCoroutine = Timing.RunCoroutine(Update(), Segment.LateUpdate);
    }

    /// <summary>
    /// Disposes of the webhook client and stops the update coroutine.
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        
        Timing.KillCoroutines(updateCoroutine);       
    }
    
    private float ParseRetryAfter(UnityWebRequest uwr)
    {
        try
        {
            var body = JsonConvert.DeserializeObject<Discord429>(uwr.downloadHandler.text);

            if (body != null && body.retry_after > 0f)
                return body.retry_after;
        }
        catch
        {
            // ignored
        }
        
        if (float.TryParse(uwr.GetResponseHeader("X-RateLimit-Retry-After"), out var headerValue))
            return headerValue;

        return 1.0f;
    }
    
    private void UpdateBuckets(UnityWebRequest uwr, string url)
    {
        var remainingStr = uwr.GetResponseHeader("X-RateLimit-Remaining");
        var resetAfterStr = uwr.GetResponseHeader("X-RateLimit-Reset-After");
        var bucketHash = uwr.GetResponseHeader("X-RateLimit-Bucket");

        if (string.IsNullOrEmpty(bucketHash) || string.IsNullOrEmpty(remainingStr))
            return;

        if (!buckets.TryGetValue(bucketHash, out var bucket))
            buckets.Add(bucketHash, bucket = new());

        urlHashes[url] = bucketHash;

        if (int.TryParse(remainingStr, out var remaining))
            bucket.Remaining = remaining;

        if (float.TryParse(resetAfterStr, out var resetAfter))
            bucket.ResetAt = Time.realtimeSinceStartup + resetAfter;
    }

    private IEnumerator<float> Update()
    {
        while (true)
        {
            yield return Timing.WaitForOneFrame;
            
            if (globalLimited && Time.realtimeSinceStartup < globalResetAt)
            {
                yield return Timing.WaitForSeconds(globalResetAt - Time.realtimeSinceStartup);
                
                globalLimited = false;
            }
            
            if (Queue.TryDequeue(out var message))
            {
                var url = message.IsEdit
                        ? DiscordMessage.GetEditUrl(Credentials.Token, Credentials.Id, message.EditId)
                        : DiscordMessage.GetPostUrl(Credentials.Token, Credentials.Id);
                
                if (urlHashes.TryGetValue(url, out var bucketHash) && buckets.TryGetValue(bucketHash, out var bucket))
                {
                    if (bucket.Remaining <= 0 && Time.realtimeSinceStartup < bucket.ResetAt)
                    {
                        yield return Timing.WaitForSeconds(bucket.ResetAt - Time.realtimeSinceStartup);
                    }
                }

                using (var request = new UnityWebRequest(url, message.IsEdit ? "PATCH" : "POST"))
                {
                    UnityWebRequestAsyncOperation op = default!;
                    
                    try
                    {
                        var json = message.Message.ToString();
                        var bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);

                        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                        request.SetRequestHeader("Content-Type", "application/json");

                        request.downloadHandler = new DownloadHandlerBuffer();
                        request.timeout = 30;

                        op = request.SendWebRequest();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }

                    if (op == null)
                        continue;
                    
                    while (!op.isDone)
                        yield return Timing.WaitForOneFrame;
                    
                    UpdateBuckets(request, url);
                    
                    if (request.responseCode == 429)
                    {
                        var retryAfter = ParseRetryAfter(request);
                        var isGlobal = request.GetResponseHeader("X-RateLimit-Global") == "true" ||
                                       request.GetResponseHeader("X-RateLimit-Scope") == "global";
                        
                        if (isGlobal)
                        {
                            globalLimited = true;
                            globalResetAt = Time.realtimeSinceStartup + retryAfter;
                        }
                        
                        Queue.Enqueue(message);

                        yield return Timing.WaitForSeconds(retryAfter);
                    }
                }
            }
        }
    }
}