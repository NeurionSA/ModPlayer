using ModPlayer.MOD;
using ModPlayer.MOD.Synth;

namespace ModPlayer
{
    internal static class Program
    {
        private static void testModPlayback(String filename)
        {
            // create a player object
            MOD.Synth.ModPlayer player = new MOD.Synth.ModPlayer();

            waitOnKey();

            Console.Clear();

            // load a file
            player.LoadMod(filename);
            // try playing
            player.Play();

            bool play = true;

            // wait for a keypress without saying we're doing so
            while (play)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Spacebar)
                {
                    player.SkipPattern();
                }
                else
                {
                    play = false;
                }

            }

            // stop playing
            player.Stop();
        }

        private static void waitOnKey()
        {
            Console.WriteLine();
            Console.Write("Press any key to continue...");
            Console.ReadKey();
        }

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Console.Title = "Synthesizer Messages";
            //Application.Run(new Form1());

            //testModPlayback("C:\\Data Alpha\\DOSBOX\\MOD\\SC2\\HYPER.MOD");
            //testModPlayback("C:\\Data Alpha\\Music\\MODs\\running_elephant.mod");
            //testModPlayback("C:\\Data Alpha\\Music\\MODs\\snail_hymn.mod");               // for testing tremolo
            //testModPlayback("C:\\Data Alpha\\Music\\MODs\\the_mission-1404.mod");         // need to test some more frequency stuff
            //testModPlayback("C:\\Data Alpha\\Music\\MODs\\darkelf_-_survive.mod");          // tone portamento issues in patterns 8-11, etc are MOSTLY fixed, I just want to give it a little more polish
            //testModPlayback("C:\\Data Alpha\\Music\\MODs\\quadrascope_ms_1.mod");           // still seems to be some frequency issues? could be a PAL vs. NTSC difference, idk
            //testModPlayback("C:\\Data Alpha\\Music\\MODs\\cpu_protect.mod");
            testModPlayback("C:\\Data Alpha\\Music\\MODs\\yavin_-_laxity_again.mod");


            Console.WriteLine("Closing, please wait...");
        }
    }
}