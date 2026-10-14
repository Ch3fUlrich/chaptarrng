using System.Collections.Generic;

namespace NzbDrone.Common.EnvironmentInfo
{
    public interface IStartupContext
    {
        HashSet<string> Flags { get; }
        Dictionary<string, string> Args { get; }
        bool Help { get; }
        bool InstallService { get; }
        bool UninstallService { get; }
        bool RegisterUrl { get; }
        bool MigrateBookshelf { get; }
        bool PreviewBookshelfMigration { get; }
        bool RestoreBookshelf { get; }
        bool PreviewBookshelfRestore { get; }
        bool ConfirmBookshelfServiceStopped { get; }

        string PreservedArguments { get; }
    }

    public class StartupContext : IStartupContext
    {
        public const string APPDATA = "data";
        public const string NO_BROWSER = "nobrowser";
        internal const string INSTALL_SERVICE = "i";
        internal const string UNINSTALL_SERVICE = "u";
        public const string HELP = "?";
        public const string TERMINATE = "terminateexisting";
        public const string RESTART = "restart";
        public const string REGISTER_URL = "registerurl";
        public const string NO_SINGLE_INSTANCE_CHECK = "nosingleinstancecheck";
        public const string EXIT_IMMEDIATELY = "exitimmediately";
        public const string MIGRATE_BOOKSHELF = "migrate-bookshelf";
        public const string PREVIEW_BOOKSHELF_MIGRATION = "preview-bookshelf-migration";
        public const string RESTORE_BOOKSHELF = "restore-bookshelf";
        public const string PREVIEW_BOOKSHELF_RESTORE = "preview-bookshelf-restore";
        public const string CONFIRM_BOOKSHELF_SERVICE_STOPPED = "confirm-bookshelf-service-stopped";

        public StartupContext(params string[] args)
        {
            Flags = new HashSet<string>();
            Args = new Dictionary<string, string>();

            foreach (var s in args)
            {
                var flag = s.Trim(' ', '/', '-');

                var argParts = flag.Split('=');

                if (argParts.Length == 2)
                {
                    Args.Add(argParts[0].Trim().ToLower(), argParts[1].Trim(' ', '"'));
                }
                else
                {
                    Flags.Add(flag.ToLower());
                }
            }
        }

        public HashSet<string> Flags { get; private set; }
        public Dictionary<string, string> Args { get; private set; }

        public bool Help => Flags.Contains(HELP);
        public bool InstallService => Flags.Contains(INSTALL_SERVICE);
        public bool UninstallService => Flags.Contains(UNINSTALL_SERVICE);
        public bool RegisterUrl => Flags.Contains(REGISTER_URL);
        public bool MigrateBookshelf => Flags.Contains(MIGRATE_BOOKSHELF);
        public bool PreviewBookshelfMigration => Flags.Contains(PREVIEW_BOOKSHELF_MIGRATION);
        public bool RestoreBookshelf => Flags.Contains(RESTORE_BOOKSHELF);
        public bool PreviewBookshelfRestore => Flags.Contains(PREVIEW_BOOKSHELF_RESTORE);
        public bool ConfirmBookshelfServiceStopped => Flags.Contains(CONFIRM_BOOKSHELF_SERVICE_STOPPED);
        public bool ExitImmediately => Flags.Contains(EXIT_IMMEDIATELY);

        public string PreservedArguments
        {
            get
            {
                var args = "";

                if (Args.ContainsKey(APPDATA))
                {
                    args = "/data=" + Args[APPDATA];
                }

                if (Flags.Contains(NO_BROWSER))
                {
                    args += " /" + NO_BROWSER;
                }

                return args.Trim();
            }
        }
    }
}
