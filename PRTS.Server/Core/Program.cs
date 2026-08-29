using System.Text;

using NiveraAPI;
using NiveraAPI.Console;
using NiveraAPI.Extensions;

using PRTS.Core.Attributes;

namespace PRTS.Core;

/// <summary>
/// Represents the main entry point of the NiveraManager.Core application.
/// This class is responsible for initializing, managing, and terminating the application's execution flow.
/// </summary>
public static class Program
{
    /// <summary>
    /// Entry point of the application that initializes, manages, and terminates the execution flow.
    /// </summary>
    public static void Main(string[] _)
    {
        InvokeHelpWriters();
        
        LibraryLoader.Exiting += OnLibExit;
        LibraryLoader.Initialize();

        try
        {
            Loader.Start();
        }
        catch (Exception ex)
        {
            ConsoleOutput.Write(ex.ToString(), ConsoleColor.Red);
        }

        while (!Loader.quit)
        {
            Thread.Sleep(1);
            
            try
            {
                LibraryUpdate.Invoke();
            }
            catch (Exception ex)
            {
                ConsoleOutput.Write(ex.ToString(), ConsoleColor.Red);
            }
        }

        if (!string.IsNullOrEmpty(Loader.message))
            ConsoleOutput.Write(Loader.message, ConsoleColor.Yellow);
        
        if (!Loader.lib) // Terminate the library if the exit wasn't raised by the library
            LibraryLoader.Exit(Loader.code);
        else
            Environment.Exit(Loader.code);
    }

    private static void OnLibExit()
    {
        Loader.lib = true;
        Loader.quit = true;
    }

    private static void InvokeHelpWriters()
    {
        try
        {
            var asm = typeof(Program).Assembly;
            var types = asm.GetTypes();

            foreach (var type in types)
            {
                try
                {
                    foreach (var method in type.GetAllMethods())
                    {
                        try
                        {
                            if (!method.HasAttribute<HelpWriterAttribute>())
                                continue;

                            if (!method.IsStatic)
                                continue;

                            if (method.ReturnType != typeof(void))
                                continue;

                            var parameters = method.GetAllParameters();

                            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(StringBuilder))
                                continue;

                            method.Invoke(null, [LibraryLoader.HelpPage]);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }
        catch
        {
            // ignored
        }
    }
}