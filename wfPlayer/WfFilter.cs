using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wfPlayer
{
    public class WfFilter 
    {
        public bool Normal { get; set; } = true;
        public bool Good { get; set; } = true;
        public bool Bad { get; set; } = true;
        public bool Dreadful { get; set; } = true;

        public int PlayCount { get; set; } = 0;
        public enum Comparison
        {
            EQ,     // ==
            LE,      // <=
            GE,      // >=
        }
        public Comparison CP { get; set; } = Comparison.EQ;

        public string Path { get; set; } = "";

        [System.Xml.Serialization.XmlIgnore]
        public string SQL
        {
            get
            {
                var sb = new StringBuilder();
                bool hasRatings = false;
                sb.Append("rating IN (");
                if(Good)
                {
                    sb.Append((long)Ratings.GOOD);
                    hasRatings = true;
                }
                if(Normal)
                {
                    if (hasRatings) sb.Append(",");
                    sb.Append((long)Ratings.NORMAL);
                    hasRatings = true;
                }
                if(Bad)
                {
                    if (hasRatings) sb.Append(",");
                    sb.Append((long)Ratings.BAD);
                    hasRatings = true;
                }
                if(Dreadful)
                {
                    if (hasRatings) sb.Append(",");
                    sb.Append((long)Ratings.DREADFUL);
                    hasRatings = true;
                }
                sb.Append(") ");
                if(!hasRatings)
                {
                    return null;
                }
                string rating = sb.ToString();
                sb.Clear();

                sb.Append("playCount ");
                switch(CP)
                {
                    case Comparison.EQ: sb.Append("="); break;
                    case Comparison.GE: sb.Append(">="); break;
                    case Comparison.LE: sb.Append("<="); break;
                }
                sb.Append(PlayCount);
                string count = sb.ToString();

                sb.Clear();

                sb.Append("WHERE ");
                sb.Append(rating);
                sb.Append(" AND ");
                sb.Append(count);

                if(!String.IsNullOrEmpty(Path))
                {
                    var s = Path.Replace("%", @"\%");
                    sb.Append($" AND path LIKE '%{s}%'");
                }

                return sb.ToString();
            }
        }

        // ComboBoxの一覧に表示するデータ
        [System.Xml.Serialization.XmlIgnore]
        public Dictionary<Comparison, string> ComparisonEnumNameDictionary { get; }
          = new Dictionary<Comparison, string>() { {Comparison.EQ, "==" }, { Comparison.GE, ">=" }, { Comparison.LE, "<=" } };

        public WfFilter Clone()
        {
            var r = new WfFilter();
            r.Normal = this.Normal;
            r.Good = this.Good;
            r.Bad = this.Bad;
            r.Dreadful= this.Dreadful;
            r.PlayCount = this.PlayCount;
            r.CP= this.CP;
            r.Path = this.Path;
            return r;
        }
    }
}
