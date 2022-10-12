﻿using System.Collections.Generic;

namespace Shoko.Server.API.v2.Models.common;

public class ObjectList
{
    public List<object> list { get; private set; }

    public string name { get; set; }
    public long size { get; set; }
    public string type { get; set; }

    public ObjectList()
    {
        list = new List<object>();
    }

    public ObjectList(string _name, ListType _type, long _size)
    {
        name = _name;
        type = _type.ToString().ToLower();
        size = _size;
    }

    public ObjectList(string _name, ListType _type)
    {
        name = _name;
        type = _type.ToString().ToLower();
        list = new List<object>();
    }

    public void Add(List<object> new_list)
    {
        list = new_list;
        size = list.Count;
    }

    public enum ListType
    {
        FILE = 1,
        EPISODE = 2,
        SERIE = 3
    }
}
