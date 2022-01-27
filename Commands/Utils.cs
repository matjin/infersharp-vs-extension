namespace infersharp_vs_ext.Commands
{
    public class Utils
    {
        private const string INFERSHARP_VERSION = "1.2";
        private const string INFERSHARP_BINARY_URL = ("https://github.com/microsoft/infersharp/releases/download/v"
                                    + INFERSHARP_VERSION + "/infersharp-linux64-v"
                                    + INFERSHARP_VERSION + ".tar.gz");
        private const string RUN_WSL_UBUNTU = "wsl ~ -d ubuntu -u root";
        private const string CORELIB_FILENAME = "/System.Private.CoreLib.dll";
        private const string CORELIB_PATH = INFERSHARP_FOLDER_NAME + "/Cilsil" + CORELIB_FILENAME;
        private const string INFER_BINARIES = INFERSHARP_FOLDER_NAME + "/infer/lib/infer/infer/bin/infer";

        public const string INFERSHARP_FOLDER_NAME = "infersharp" + INFERSHARP_VERSION;
        public const string INFERSHARP_TAR_GZ = "infersharp.tar.gz";
        public const string INFER_OUT = "infer-out/";
        public const string INSTALL_WSL_UBUNTU  = "wsl --install -d ubuntu";
        public const string INFERSHARP_FOLDER = "infersharp" + INFERSHARP_VERSION;
        public const string RUN_WSL = "wsl ~ -u root";
        public const string TRY_GET_INFERSHARP_BINARIES = ("do {" + RUN_WSL_UBUNTU +" wget " + INFERSHARP_BINARY_URL + " -O " + INFERSHARP_TAR_GZ + ";" + 
                                                    "$success =$?; " + 
                                                    "if (-not $success) { $MaxAttempts++ }; " + 
                                                    "if ($MaxAttempts -ge 10) { " +
                                                        "'Automatic install timeout -- please see manual setup steps'; exit; "+
                                                    "};" + 
                                                    "Start-Sleep -s 5;" + 
                                                "} " +
                                                "until ($success);");
        public const string SET_WSL_DEFAULT_UBUNTU = "wsl -s ubuntu";
        public const string MONITOR = ("do { Start-Sleep -s 60; $count = " + RUN_WSL_UBUNTU + 
                                            " grep -wc 'Elapsed analysis time:' " + INFER_OUT + 
                                            "logs; 'Methods analyzed: ' + $count.Split(' ')[0]; " + 
                                        "} while ($true);");

        public static string Copy(string sourcePath, string destinationPath)
        {
            return "cp " + sourcePath + " " + destinationPath;
        }

        public static string Move(string sourcePath, string destinationPath)
        {
            return "mv " + sourcePath + " " + destinationPath;
        }

        public static string Remove(string path)
        {
            return "rm -rf " + path;
        }

        public static string Print(string output)
        {
            return "echo '" + output + "'";
        }

        public static string RunWsl(string command)
        {
            return "wsl ~ -u root " + command;
        }

        public static string RunWslUbuntu(string command)
        {
            return "wsl ~ -u root -d ubuntu " + command;
        }

        public static string TranslateAndMove(string inputPath)
        {
            var coreLibCopy = inputPath + CORELIB_FILENAME;

            var getCoreLib = Copy(CORELIB_PATH, coreLibCopy);
            var translate = (INFERSHARP_FOLDER_NAME + "/Cilsil/Cilsil translate " + inputPath +
                            " --outcfg " + inputPath + "/cfg.json " +
                            " --outtenv " + inputPath + "/tenv.json " + "--extprogress");
            var moveCfg = Move(inputPath + "/cfg.json", "~/cfg.json");
            var moveTenv = Move(inputPath + "/tenv.json", "~/tenv.json");
            var removeCoreLibCopy = Remove(coreLibCopy);
            var removeOldOutput = Remove(inputPath + "/" + INFER_OUT);
            string[] commands = { RunWsl(getCoreLib), RunWsl(translate),
                                  RunWsl(moveCfg), RunWsl(moveTenv),
                                  RunWsl(removeCoreLibCopy), RunWsl(removeOldOutput) };
            return string.Join(";", commands);
        }

        public static string InferAnalyze(string inputPath)
        {
            var capture = INFER_BINARIES + " capture";
            var makeCaptured = "mkdir -p infer-out/captured";
            var inferAnalyzeJson = (INFER_BINARIES + " analyzejson " +
                                    " --debug-level 1 --pulse " +
                                    "--no-biabduction --sarif " +
                                    "--disable-issue-type PULSE_UNINITIALIZED_VALUE " +
                                    "--disable-issue-type MEMORY_LEAK " +
                                    "--disable-issue-type UNINITIALIZED_VALUE " +
                                    "--cfg-json cfg.json --tenv-json tenv.json");
            var moveOutput = "cp -r ~/infer-out/ " + inputPath;
            string[] commands = { RunWsl(capture), RunWsl(makeCaptured),
                                  RunWsl(inferAnalyzeJson), RunWsl(moveOutput) };
            return string.Join(";", commands);
        }
    }
}
