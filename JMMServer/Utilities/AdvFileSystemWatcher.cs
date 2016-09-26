using System;
using System.Collections.Generic;
using System.IO;

namespace JMMServer
{
    public class AdvFileSystemWatcher : FileSystemWatcher, IDisposable
    {
        #region IDisposable Members

        public new void Dispose()
        {
            base.Dispose();
        }

        #endregion

        #region Private Members

        // This Dictionary keeps the track of when an event occured last for a particular file
        private Dictionary<string, DateTime> _lastFileEvent;
        // Interval in Millisecond
        private int _interval;
        //Timespan created when interval is set
        private TimeSpan _recentTimeSpan;

        #endregion

        #region Constructors

        // Constructors delegate to the base class constructors and call private InitializeMember method
        public AdvFileSystemWatcher()
        {
            InitializeMembers();
        }

        public AdvFileSystemWatcher(string Path)
          : base(Path)
        {
            InitializeMembers();
        }

        public AdvFileSystemWatcher(string Path, string Filter)
          : base(Path, Filter)
        {
            InitializeMembers();
        }

        #endregion

        #region Events

        // These events hide the events from the base class. 
        // We want to raise these events appropriately and we do not want the 
        // users of this class subscribing to these events of the base class accidentally
        public new event FileSystemEventHandler Changed;
        public new event FileSystemEventHandler Created;
        public new event FileSystemEventHandler Deleted;
        public new event RenamedEventHandler Renamed;

        #endregion

        #region Protected Methods

        // Protected Methods to raise the Events for this class
        protected new virtual void OnChanged(FileSystemEventArgs e)
        {
            if (Changed != null) Changed(this, e);
        }

        protected new virtual void OnCreated(FileSystemEventArgs e)
        {
            if (Created != null) Created(this, e);
        }

        protected new virtual void OnDeleted(FileSystemEventArgs e)
        {
            if (Deleted != null) Deleted(this, e);
        }

        protected new virtual void OnRenamed(RenamedEventArgs e)
        {
            if (Renamed != null) Renamed(this, e);
        }

        #endregion

        #region Private Methods

        /// <summary>
        ///   This Method Initializes the private members.
        ///   Interval is set to its default value of 100 millisecond
        ///   FilterRecentEvents is set to true, _lastFileEvent dictionary is initialized
        ///   We subscribe to the base class events.
        /// </summary>
        private void InitializeMembers()
        {
            Interval = 100;
            FilterRecentEvents = true;
            _lastFileEvent = new Dictionary<string, DateTime>();

            base.Created += OnCreated;
            base.Changed += OnChanged;
            base.Deleted += OnDeleted;
            base.Renamed += OnRenamed;
        }

        /// <summary>
        ///   This method searches the dictionary to find out when the last event occured
        ///   for a particular file. If that event occured within the specified timespan
        ///   it returns true, else false
        /// </summary>
        /// <param name="FileName">The filename to be checked</param>
        /// <returns>True if an event has occured within the specified interval, False otherwise</returns>
        private bool HasAnotherFileEventOccuredRecently(string FileName)
        {
            var retVal = false;

            // Check dictionary only if user wants to filter recent events otherwise return Value stays False
            if (FilterRecentEvents)
            {
                if (_lastFileEvent.ContainsKey(FileName))
                {
                    // If dictionary contains the filename, check how much time has elapsed
                    // since the last event occured. If the timespan is less that the 
                    // specified interval, set return value to true 
                    // and store current datetime in dictionary for this file
                    var lastEventTime = _lastFileEvent[FileName];
                    var currentTime = DateTime.Now;
                    var timeSinceLastEvent = currentTime - lastEventTime;
                    retVal = timeSinceLastEvent < _recentTimeSpan;
                    _lastFileEvent[FileName] = currentTime;
                }
                else
                {
                    // If dictionary does not contain the filename, 
                    // no event has occured in past for this file, so set return value to false
                    // and annd filename alongwith current datetime to the dictionary
                    _lastFileEvent.Add(FileName, DateTime.Now);
                    retVal = false;
                }
            }

            return retVal;
        }

        #region FileSystemWatcher EventHandlers

        // Base class Event Handlers. Check if an event has occured recently and call method
        // to raise appropriate event only if no recent event is detected
        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (!HasAnotherFileEventOccuredRecently(e.FullPath))
                OnChanged(e);
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (!HasAnotherFileEventOccuredRecently(e.FullPath))
                OnCreated(e);
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            if (!HasAnotherFileEventOccuredRecently(e.FullPath))
                OnDeleted(e);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (!HasAnotherFileEventOccuredRecently(e.OldFullPath))
                OnRenamed(e);
        }

        #endregion

        #endregion

        #region Public Properties

        /// <summary>
        ///   Interval, in milliseconds, within which events are considered "recent"
        /// </summary>
        public int Interval
        {
            get { return _interval; }
            set
            {
                _interval = value;
                // Set timespan based on the value passed
                _recentTimeSpan = new TimeSpan(0, 0, 0, 0, value);
            }
        }

        /// <summary>
        ///   Allows user to set whether to filter recent events. If this is set a false,
        ///   this class behaves like System.IO.FileSystemWatcher class
        /// </summary>
        public bool FilterRecentEvents { get; set; }

        #endregion
    }
}