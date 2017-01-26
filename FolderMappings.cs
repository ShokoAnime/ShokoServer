using System;
using System.Collections.Generic;
using Pri.LongPath;
using Shoko.Models.Server;
using Directory = Pri.LongPath.Directory;

namespace Shoko.Commons
{
    public class FolderMappings
    {
        public static FolderMappings Instance { get; }=new FolderMappings();

        private Dictionary<int, string> _mappings = new Dictionary<int, string>();
        private Action<Dictionary<int, string>> _saveFunction;
        private Func<Dictionary<int, string>> _loadFunction;
        private bool _wasloaded;

        private void LoadCheck()
        {
            if (_wasloaded)
                return;
            if (_loadFunction != null)
            {
                _mappings = _loadFunction();
                _wasloaded = true;
            }
        }

        public void SetLoadAndSaveCallback(Func<Dictionary<int, string>> loadFunction, Action<Dictionary<int, string>> saveFunction)
        {
            _loadFunction = loadFunction; 
            _saveFunction = saveFunction;
        }

        public void MapFolder(int folderid, string localpath)
        {
            LoadCheck();
            _mappings[folderid] = localpath;
            _saveFunction?.Invoke(_mappings);
        }

        public void UnMapFolder(int folderid)
        {
            LoadCheck();
            if (_mappings.ContainsKey(folderid))
                _mappings.Remove(folderid);
            _saveFunction?.Invoke(_mappings);
        }

        public string GetMapping(int folderid)
        {
            if (_mappings.ContainsKey(folderid))
                return _mappings[folderid];
            return string.Empty;
        }

        public bool IsValid(int folderid)
        {
            try
            {
                if (_mappings.ContainsKey(folderid))
                {
                    if (Directory.Exists(_mappings[folderid]))
                        return true;
                }
            }
            catch (Exception e)
            {
                //Security issue, TODO
            }
            return false;
        }
        public string TranslateFile(ImportFolder impfolder, string path)
        {
            if (impfolder.CloudID.HasValue)
                return string.Empty;
            return TranslateFile(impfolder.ImportFolderID, path);
        }
        public string TranslateDirectory(ImportFolder impfolder, string path)
        {
            if (impfolder.CloudID.HasValue)
                return string.Empty;
            return TranslateDirectory(impfolder.ImportFolderID, path);
        }

        public string TranslateFile(int folderid, string path)
        {
            LoadCheck();
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            if (!_mappings.ContainsKey(folderid))
                return string.Empty;
            string start = Path.Combine(_mappings[folderid], path);
            try
            {
                if (File.Exists(start))
                    return start;
            }
            catch (Exception e)
            {
                //Security issue, TODO
            }
            return string.Empty;
        }

        public string TranslateDirectory(int folderid, string path)
        {
            LoadCheck();
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            if (!_mappings.ContainsKey(folderid))
                return string.Empty;
            string start = Path.Combine(_mappings[folderid], path);
            try
            {
                if (Directory.Exists(start))
                    return start;
            }
            catch (Exception e)
            {
                //Security issue, TODO
            }
            return string.Empty;
        }
    }
}
