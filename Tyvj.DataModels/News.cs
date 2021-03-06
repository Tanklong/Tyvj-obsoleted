﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations.Schema;

namespace Tyvj.DataModels
{
    [Table("news")]
    public class News
    {
        [Column("id")]
        public int ID { get; set; }

        [Column("title")]
        public string Title { get; set; }

        [Column("time")]
        public DateTime Time { get; set; }

        [Column("content")]
        public string Content { get; set; }

        public override bool Equals(object obj)
        {
            var data = obj as News;
            if (data.ID == this.ID) return true;
            return false;
        }

        public override int GetHashCode()
        {
            return this.ID;
        }
    }
}