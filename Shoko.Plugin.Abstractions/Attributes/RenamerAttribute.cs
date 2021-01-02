﻿using System;

namespace Shoko.Plugin.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RenamerAttribute : Attribute
    {
        public RenamerAttribute(string renamerId)
        {
            RenamerId = renamerId;
        }

        public string RenamerId { get; }
        private string _desc;

        public string Description
        {
            get => _desc ?? RenamerId;
            set => _desc = value;
        }
    }
}