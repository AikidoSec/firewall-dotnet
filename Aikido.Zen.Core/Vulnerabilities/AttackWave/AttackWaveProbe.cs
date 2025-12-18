using System;
using System.Collections.Generic;
using System.Linq;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Vulnerabilities
{
    internal static class AttackWaveProbe
    {
        private static readonly HashSet<string> Methods = new HashSet<string>(
            new[] { "BADMETHOD", "BADHTTPMETHOD", "BADDATA", "BADMTHD", "BDMTHD" },
            StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> FileExtensions = new HashSet<string>(
            new[] { "env", "bak", "sql", "sqlite", "sqlite3", "db", "old", "save", "orig", "sqlitedb", "sqlite3db" },
            StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> FileNames = new HashSet<string>(
            new[] {
            ".addressbook",".atom",".bashrc",".boto",".config",".config.json",".config.xml",".config.yaml",".config.yml",".envrc",
            ".eslintignore",".fbcindex",".forward",".gitattributes",".gitconfig",".gitignore",".gitkeep",".gitlab-ci.yaml",
            ".gitlab-ci.yml",".gitmodules",".google_authenticator",".hgignore",".htaccess",".htpasswd",".htdigest",".ksh_history",
            ".lesshst",".lhistory",".lighttpdpassword",".lldb-history",".lynx_cookies",".my.cnf",".mysql_history",".nano_history",
            ".netrc",".node_repl_history",".npmrc",".nsconfig",".nsr",".password-store",".pearrc",".pgpass",".php_history",".pinerc",
            ".proclog",".procmailrc",".profile",".psql_history",".python_history",".rediscli_history",".rhosts",".selected_editor",
            ".sh_history",".sqlite_history",".svnignore",".tcshrc",".tmux.conf",".travis.yaml",".travis.yml",".viminfo",".vimrc",
            ".www_acl",".wwwacl",".xauthority",".yarnrc",".zhistory",".zsh_history",".zshenv",".zshrc","Dockerfile","aws-key.yaml",
            "aws-key.yml","aws.yaml","aws.yml","docker-compose.yaml","docker-compose.yml","npm-shrinkwrap.json","package-lock.json",
            "package.json","phpinfo.php","wp-config.php","wp-config.php3","wp-config.php4","wp-config.php5","wp-config.phtml",
            "composer.json","composer.lock","composer.phar","yarn.lock",".env.local",".env.development",".env.test",
            ".env.production",".env.prod",".env.dev",".env.example","php.ini","wp-settings.php","config.asp","config_dev.asp",
            "config-dev.asp","config.dev.asp","config_prod.asp","config-prod.asp","config.prod.asp","config.sample.asp",
            "config-sample.asp","config_sample.asp","config_test.asp","config-test.asp","config.test.asp","config.ini","config_dev.ini",
            "config-dev.ini","config.dev.ini","config_prod.ini","config-prod.ini","config.prod.ini","config.sample.ini",
            "config-sample.ini","config_sample.ini","config_test.ini","config-test.ini","config.test.ini","config.json","config_dev.json",
            "config-dev.json","config.dev.json","config_prod.json","config-prod.json","config.prod.json","config.sample.json",
            "config-sample.json","config_sample.json","config_test.json","config-test.json","config.test.json","config.php",
            "config_dev.php","config-dev.php","config.dev.php","config_prod.php","config-prod.php","config.prod.php","config.sample.php",
            "config-sample.php","config_sample.php","config_test.php","config-test.php","config.test.php","config.pl","config_dev.pl",
            "config-dev.pl","config.dev.pl","config_prod.pl","config-prod.pl","config.prod.pl","config.sample.pl","config-sample.pl",
            "config_sample.pl","config_test.pl","config-test.pl","config.test.pl","config.py","config_dev.py","config-dev.py",
            "config.dev.py","config_prod.py","config-prod.py","config.prod.py","config.sample.py","config-sample.py","config_sample.py",
            "config_test.py","config-test.py","config.test.py","config.rb","config_dev.rb","config-dev.rb","config.dev.rb",
            "config_prod.rb","config-prod.rb","config.prod.rb","config.sample.rb","config-sample.rb","config_sample.rb","config_test.rb",
            "config-test.rb","config.test.rb","config.toml","config_dev.toml","config-dev.toml","config.dev.toml","config_prod.toml",
            "config-prod.toml","config.prod.toml","config.sample.toml","config-sample.toml","config_sample.toml","config_test.toml",
            "config-test.toml","config.test.toml","config.txt","config_dev.txt","config-dev.txt","config.dev.txt","config_prod.txt",
            "config-prod.txt","config.prod.txt","config.sample.txt","config-sample.txt","config_sample.txt","config_test.txt",
            "config-test.txt","config.test.txt","config.xml","config_dev.xml","config-dev.xml","config.dev.xml","config_prod.xml",
            "config-prod.xml","config.prod.xml","config.sample.xml","config-sample.xml","config_sample.xml","config_test.xml",
            "config-test.xml","config.test.xml","config.yaml","config_dev.yaml","config-dev.yaml","config.dev.yaml","config_prod.yaml",
            "config-prod.yaml","config.prod.yaml","config.sample.yaml","config-sample.yaml","config_sample.yaml","config_test.yaml",
            "config-test.yaml","config.test.yaml","config.yml","config_dev.yml","config-dev.yml","config.dev.yml","config_prod.yml",
            "config-prod.yml","config.prod.yml","config.sample.yml","config-sample.yml","config_sample.yml","config_test.yml",
            "config-test.yml","config.test.yml","boot.ini","gruntfile.js","localsettings.php","my.ini","npm-debug.log","parameters.yml",
            "parameters.yaml","services.yml","services.yaml","web.config","webpack.config.js","config.old","config.inc.php","error.log",
            "access.log",".DS_Store","passwd","win.ini","cmd.exe","my.cnf",".bash_history","docker-compose-dev.yml",
            "docker-compose.override.yml","docker-compose.dev.yml","Cargo.lock","secrets.yml","secrets.yaml","docker-compose.staging.yml",
            "docker-compose.production.yml","yaws-key.pem","mysql_config.ini","firewall.log","log4j.properties",
            "serviceAccountCredentials.json","haproxy.cfg","service-account-credentials.json","vpn.log","system.log","webuser-auth.xml",
            "fastcgi.conf","smb.conf","iis.log","pom.xml","openapi.json","vim_settings.xml","winscp.ini","ws_ftp.ini",
        }, StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> Directories = new HashSet<string>(
            new[] {
            ".","..",".anydesk",".aptitude",".aws",".azure",".cache",".circleci",".config",".dbus",".docker",".drush",".gem",
            ".git",".github",".gnupg",".gsutil",".hg",".idea",".java",".kube",".lftp",".minikube",".npm",".nvm",".pki",".snap",
            ".ssh",".subversion",".svn",".tconn",".thunderbird",".tor",".vagrant.d",".vidalia",".vim",".vmware",".vscode",
            "apache","apache2","grub","System32","tmp","xampp","cgi-bin","%systemroot%",
        }, StringComparer.OrdinalIgnoreCase);

        private static readonly string[] Keywords =
        {
            "SELECT (CASE WHEN",
            "SELECT COUNT(",
            "SLEEP(",
            "WAITFOR DELAY",
            "SELECT LIKE(CHAR(",
            "INFORMATION_SCHEMA.COLUMNS",
            "INFORMATION_SCHEMA.TABLES",
            "MD5(",
            "DBMS_PIPE.RECEIVE_MESSAGE",
            "SYSIBM.SYSTABLES",
            "RANDOMBLOB(",
            "SELECT * FROM",
            "1'='1",
            "PG_SLEEP(",
            "UNION ALL SELECT",
            "../",
        };

        public static bool IsProbeRequest(Context context)
        {
            if (context == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(context.Method) && IsProbeMethod(context.Method))
            {
                return true;
            }

            var path = string.IsNullOrEmpty(context.Route) ? context.Url : context.Route;
            if (!string.IsNullOrEmpty(path) && IsProbePath(path))
            {
                return true;
            }

            if (QueryParamsContainDangerousPayload(context.Query))
            {
                return true;
            }

            return false;
        }

        internal static bool IsProbeMethod(string method)
        {
            return Methods.Contains(method ?? string.Empty);
        }

        internal static bool IsProbePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var normalized = path;
            var questionMarkIndex = normalized.IndexOf("?", StringComparison.Ordinal);
            if (questionMarkIndex >= 0)
            {
                normalized = normalized.Substring(0, questionMarkIndex);
            }
            var hashIndex = normalized.IndexOf("#", StringComparison.Ordinal);
            if (hashIndex >= 0)
            {
                normalized = normalized.Substring(0, hashIndex);
            }

            normalized = normalized.ToLowerInvariant();

            var segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var filename = segments.LastOrDefault();

            if (!string.IsNullOrEmpty(filename))
            {
                if (FileNames.Contains(filename))
                {
                    return true;
                }

                if (filename.Contains('.'))
                {
                    var ext = filename.Split('.').LastOrDefault();
                    if (!string.IsNullOrEmpty(ext) && FileExtensions.Contains(ext))
                    {
                        return true;
                    }
                }

                segments.RemoveAt(segments.Count - 1);
            }

            foreach (var dir in segments)
            {
                if (Directories.Contains(dir))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool QueryParamsContainDangerousPayload(IDictionary<string, string> query)
        {
            if (query == null || query.Count == 0)
            {
                return false;
            }

            foreach (var str in ExtractStringsFromQuery(query))
            {
                if (str.Length < 5 || str.Length > 1000)
                {
                    continue;
                }

                var upper = str.ToUpperInvariant();
                foreach (var keyword in Keywords)
                {
                    if (upper.IndexOf(keyword, StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static IEnumerable<string> ExtractStringsFromQuery(IDictionary<string, string> query)
        {
            foreach (var pair in query)
            {
                if (!string.IsNullOrEmpty(pair.Key))
                {
                    yield return pair.Key;
                }

                if (!string.IsNullOrEmpty(pair.Value))
                {
                    yield return pair.Value;
                }
            }
        }
    }
}
