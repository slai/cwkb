using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;

namespace cwkb
{
    // from http://ndjango.org/index.php?title=Library
    class FileSystemTemplateLoader : NDjango.Interfaces.ITemplateLoader
    {
        internal FileSystemTemplateLoader()
        {
            rootDir = HttpRuntime.AppDomainAppPath;
        }

        string rootDir;
        #region ITemplateLoader Members

        public TextReader GetTemplate(string name)
        {
            return File.OpenText(Path.Combine(rootDir, name));
        }

        public bool IsUpdated(string name, System.DateTime timestamp)
        {
            return File.GetLastWriteTime(Path.Combine(rootDir, name)) > timestamp;
        }

        #endregion
    }
}