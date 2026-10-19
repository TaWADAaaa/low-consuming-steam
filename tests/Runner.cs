using System;

namespace Tawop.Tests
{
    public static class Runner
    {
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length != 1)
                {
                    throw new ArgumentException("Expected package root.");
                }

                AssetStoreTests.Run();
                Console.WriteLine("Standalone embedded asset checks passed.");
                AggressivePolicyTests.Run();
                Console.WriteLine("Aggressive application policy checks passed.");
                CancellationTests.Run(args[0]);
                Console.WriteLine("Cooperative cancellation and recovery checks passed.");
                AutoSetupTests.Run(args[0]);
                Console.WriteLine("Automatic detection, setup and recovery checks passed.");
                AsarTests.Run();
                Console.WriteLine("ASAR + Discord checks passed.");
                SteamTests.Run();
                Console.WriteLine("Steam resource checks passed.");
                ChromeTests.Run();
                Console.WriteLine("Chromium source checks passed.");
                ChromeRuntimeProfileTests.Run();
                Console.WriteLine("Installed Chrome policy and recovery fixtures passed.");
                CoreTests.Run(args[0]);
                Console.WriteLine("Transactional patch checks passed.");
                ReviewTests.Run();
                Console.WriteLine("Independent recovery checks passed.");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine(error);
                return 1;
            }
        }
    }
}
