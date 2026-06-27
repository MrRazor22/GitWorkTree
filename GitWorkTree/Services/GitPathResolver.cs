using System;
using System.IO;

namespace GitWorkTree.Services
{
    public class GitPathResolver : IGitPathResolver
    {
        private readonly string _gitPath;

        public GitPathResolver()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string vs2017plusGit = Path.Combine(baseDir, @"CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe");
            string vs2015Git = Path.Combine(baseDir, @"Extensions\3rdParty\Git\cmd\git.exe");

            if (File.Exists(vs2017plusGit))
            {
                _gitPath = vs2017plusGit;
            }
            else if (File.Exists(vs2015Git))
            {
                _gitPath = vs2015Git;
            }
            else
            {
                _gitPath = ResolveFromEnvironmentPath() ?? "git.exe";
            }
        }

        public string GetGitPath() => _gitPath;

        private string ResolveFromEnvironmentPath()
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv)) return null;

            foreach (string pathDir in pathEnv.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(pathDir)) continue;
                try
                {
                    string fullPath = Path.Combine(pathDir.Trim(), "git.exe");
                    if (File.Exists(fullPath)) return fullPath;
                }
                catch { }
            }
            return null;
        }
    }
}
