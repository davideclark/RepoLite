﻿using RepoLite.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RepoLite.Common.Models
{
    public class Table
    {
        public List<Column> Columns { get; set; } = new List<Column>();

        public string Schema { get; set; }
        //public string SequenceName;
        //public bool Ignore;
        public string DbTableName { get; set; }

        public string LowerClassName
        {
            get
            {
                var name = DbTableName.ToLower();
                if (Helpers.ReservedWord(name))
                    name = "@" + name;

                return name;
            }
        }

        public string ClassName
        {
            get
            {
                var name = DbTableName;
                if (Helpers.ReservedWord(name))
                    name = "@" + name;

                return name;
            }
        }

        public Column GetColumn(string columnName)
        {
            return Columns.Single(x => string.Compare(x.PropertyName, columnName, StringComparison.OrdinalIgnoreCase) == 0);
        }

        public Column this[string columnName] => GetColumn(columnName);

        public PrimaryKeyConfigurationEnum PrimaryKeyConfiguration
        {
            get
            {
                var keyCount = Columns.Count(x => x.PrimaryKey);

                switch (keyCount)
                {
                    case 0:
                        return PrimaryKeyConfigurationEnum.NoKey;
                    case 1:
                        return PrimaryKeyConfigurationEnum.PrimaryKey;
                    default:
                        return PrimaryKeyConfigurationEnum.CompositeKey;
                }
            }
        }

        public List<Column> PrimaryKeys
        {
            get { return Columns.Where(x => x.PrimaryKey).ToList(); }
        }

        public List<Column> NonPrimaryKeys
        {
            get { return Columns.Where(x => !x.PrimaryKey).ToList(); }
        }
    }
}
