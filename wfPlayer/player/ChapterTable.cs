using io.github.toyota32k.toolkit.view;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wfPlayer {
    public class ChapterInfo : PropertyChangeNotifier {
        private bool mSkip;
        private string mLabel;

        public bool IsModified { get; private set; } = false;
        public void ResetModifiedFlag() { IsModified = false; }

        public ulong Position { get; private set; }

        public string Label {
            get => mLabel;
            set {
                if (setProp(callerName(), ref mLabel, value)) {
                    IsModified = true;
                }
            }
        }

        public bool Skip {
            get => mSkip;
            set {
                if (setProp(callerName(), ref mSkip, value)) {
                    IsModified = true;
                }
            }
        }
        public ChapterInfo(ulong pos, bool skip = false, string label = null) {
            Position = pos;
            mSkip = skip;
            mLabel = label;
        }

        static public string FormatDuration(ulong duration) {
            var t = TimeSpan.FromMilliseconds(duration);
            return string.Format("{0}:{1:00}:{2:00}", t.Hours, t.Minutes, t.Seconds);
        }

        public string PositionText => FormatDuration(Position);

        private ulong mLength = 0;
        public ulong Length {
            get => mLength;
            set => setProp(callerName(), ref mLength, value, "LengthText");
        }
        public string LengthText => FormatDuration(mLength);

        private int mIndex = 0;
        public int Index {
            get => mIndex;
            set => setProp(callerName(), ref mIndex, value);
        }
    }

    public class ChapterEntry {
        private ulong? id { get; set; } = null;
        public ulong Owner { get; private set; }
        public ulong Position { get; private set; }
        public string Label { get; set; }

        public bool Skip { get; set; }

        public ChapterEntry() {
            Owner = 0;
            Position = 0;
            Label = null;
            Skip = false;
        }

        static public ChapterEntry Create(ulong owner, ulong pos, bool skip = false, string label = null) {
            return new ChapterEntry() { Owner = owner, Position = pos, Skip = skip, Label = label };
        }
        static public ChapterEntry Create(ulong owner, ChapterInfo info) {
            return new ChapterEntry() { Owner = owner, Position = info.Position, Skip = info.Skip, Label = info.Label };
        }

        static public ChapterEntry FromReader(SQLiteDataReader reader) {
            return new ChapterEntry() {
                id = Convert.ToUInt64(reader["id"]),
                Owner = Convert.ToUInt64(reader["owner"]),
                Position = Convert.ToUInt64(reader["position"]),
                Label = reader["label"].ToString(),
                Skip = Convert.ToBoolean(reader["skip"])
            };
        }

        public ChapterInfo ToChapterInfo() {
            return new ChapterInfo(Position, Skip, Label);
        }
    }

    public interface IPlayRange {
        ulong Start { get; }
        ulong End { get; }
    }
    public struct PlayRange : IPlayRange {
        //private ulong mStart = 0;
        //private ulong mEnd = 0;
        public ulong Start { get; private set; }
        public ulong End { get; private set; }

        static public PlayRange Empty => new PlayRange(0, 0);

        public PlayRange(ulong start, ulong end = 0) {
            if (end == 0) {
                Start = start;
                End = 0;
            } else {
                if (start > end) {
                    Start = end;
                    End = start;
                } else {
                    Start = start;
                    End = end;
                }
            }
        }

        public PlayRange Clone() {
            return new PlayRange(Start, End);
        }

        public void Set(ulong start, ulong end) {
            this = new PlayRange(start, end);
        }

        public bool TrySetStart(ulong start) {
            if (start != Start && (End == 0 || start < End)) {
                Start = start;
                return true;
            }
            return false;
        }

        public bool TrySetEnd(ulong end) {
            if (end != End && (end == 0 || Start < end)) {
                End = end;
                return true;
            }
            return false;
        }

        public bool Contains(ulong value) {
            return Start <= value && (End == 0 || value < End);
        }

    }


    public class ChapterList {
        public ObservableCollection<ChapterInfo> Values { get; } = new ObservableCollection<ChapterInfo>();
        const ulong MIN_CHAPTER_SPAN = 1000;
        public ulong Owner { get; }

        private bool mIsModified = false;
        public bool IsModified {
            get { return mIsModified || Values.Where((c) => c.IsModified).Any(); }
            private set { mIsModified = value; }
        }
        public ChapterList(ulong owner, IEnumerable<ChapterInfo> src) {
            Owner = owner;
            foreach (var c in src) {
                AddChapter(c);
            }
        }

        public void ResetModifiedFlag() {
            mIsModified = false;
            foreach (var c in Values) {
                c.ResetModifiedFlag();
            }
        }

        public bool CanAddChapter(ulong pos) {
            if (GetNeighbourChapterIndex(pos, out var prev, out var next)) {
                return false;
            }
            return CanAddChapter(pos, prev, next);
        }
        private bool CanAddChapter(ulong pos, int prev, int next) {
            ulong diff(ulong a, ulong b) {
                return a < b ? b - a : a - b;
            }
            if (prev >= 0 && diff(Values[prev].Position, pos) < MIN_CHAPTER_SPAN) {
                return false;
            }
            if (next >= 0 && diff(Values[next].Position, pos) < MIN_CHAPTER_SPAN) {
                return false;
            }
            return true;
        }

        public bool AddChapter(ChapterInfo chapter) {
            int prev, next;
            if (GetNeighbourChapterIndex(chapter.Position, out prev, out next)) {
                return false;
            }
            if (!CanAddChapter(chapter.Position)) {
                return false;
            }
            if (next < 0) {
                Values.Add(chapter);
            } else {
                Values.Insert(next, chapter);
            }
            IsModified = true;
            return true;
        }

        public bool RemoveChapter(ChapterInfo chapter) {
            int prev, next;
            if (!GetNeighbourChapterIndex(chapter.Position, out prev, out next)) {
                return false;
            }
            Values.RemoveAt(prev + 1);
            IsModified = true;
            return true;
        }

        public bool ClearAllChapters() {
            if (Values.Count > 0) {
                Values.Clear();
                IsModified = true;
                return true;
            }
            return false;
        }
        /**
            * 指定位置(current)近傍のChapterを取得
            * 
            * @param prev (out) currentの前のChapter（なければ-1）
            * @param next (out) currentの次のChapter（なければ-1）
            * @return true: currentがマーカー位置にヒット（prevとnextにひとつ前/後のindexがセットされる）
            *         false: ヒットしていない
            */
        public bool GetNeighbourChapterIndex(ulong current, out int prev, out int next) {
            int count = Values.Count;
            int clipIndex(int index) {
                return (0 <= index && index < count) ? index : -1;
            }
            for (int i = 0; i < count; i++) {
                if (current == Values[i].Position) {
                    prev = i - 1;
                    next = clipIndex(i + 1);
                    return true;
                }
                if (current < Values[i].Position) {
                    prev = i - 1;
                    next = i;
                    return false;
                }
            }
            prev = count - 1;
            next = -1;
            return false;
        }

        private IEnumerable<PlayRange> GetDisabledChapterRanges() {
            bool skip = false;
            ulong skipStart = 0;

            foreach (var c in Values) {
                if (c.Skip) {
                    if (!skip) {
                        skip = true;
                        skipStart = c.Position;
                    }
                } else {
                    if (skip) {
                        skip = false;
                        yield return new PlayRange(skipStart, c.Position);
                    }
                }
            }
            if (skip) {
                yield return new PlayRange(skipStart, 0);
            }
        }

        public IEnumerable<PlayRange> GetDisabledRanges(PlayRange trimming) {
            var trimStart = trimming.Start;
            var trimEnd = trimming.End;
            foreach (var r in GetDisabledChapterRanges()) {
                if (r.End < trimming.Start) {
                    // ignore
                    continue;
                } else if (trimStart > 0) {
                    if (r.Start < trimStart) {
                        yield return new PlayRange(0, r.End);
                        continue;
                    } else {
                        yield return new PlayRange(0, trimStart);
                    }
                    trimStart = 0;
                }

                if (trimEnd > 0) {
                    if (trimEnd < r.Start) {
                        break;
                    } else if (trimEnd < r.End) {
                        trimEnd = 0;
                        yield return new PlayRange(r.Start, 0);
                        break;
                    }
                }
                yield return r;
            }
            if (trimStart > 0) {
                yield return new PlayRange(0, trimStart);
            }
            if (trimEnd > 0) {
                yield return new PlayRange(trimEnd, 0);
            }
        }
    }

    class ChapterGroup {
        public string Owner { get; }
        public List<ChapterEntry> Chapters { get; }
        public ChapterGroup(string owner, List<ChapterEntry> list) {
            Owner = owner;
            Chapters = list;
        }
    };

    public class ChapterTable {
        private WeakReference<SQLiteConnection> mRefDB;
        private SQLiteConnection DB => mRefDB?.GetValue() ?? null;

        public ChapterTable(SQLiteConnection db) {
            mRefDB = new WeakReference<SQLiteConnection>(db);
        }

        public bool Contains(ChapterEntry entry) {
            var cmd = DB.CreateCommand();
            cmd.CommandText = $"SELECT 1 from t_chapter WHERE owner='{entry.Owner}' AND position='{entry.Position}'";
            using (var reader = cmd.ExecuteReader()) {
                return reader.NextResult();
            }
        }

        public ChapterEntry Get(ulong owner, ulong position) {
            var cmd = DB.CreateCommand();
            cmd.CommandText = $"SELECT * from t_chapter WHERE owner='{owner}' AND position='{position}'";
            using (var reader = cmd.ExecuteReader()) {
                if(reader.Read()) {
                    return ChapterEntry.FromReader(reader);
                }
            }
            return null;
        }

        public ChapterEntry Delete(ulong owner, ulong position) {
            var cmd = DB.CreateCommand();
            cmd.CommandText = $"DELETE FROM t_chapter WHERE owner='{owner}' AND position='{position}'";
            using (var reader = cmd.ExecuteReader()) {
                if (reader.Read()) {
                    return ChapterEntry.FromReader(reader);
                }
            }
            return null;
        }

        public IEnumerable<ChapterEntry> GetChapterEntries(ulong owner) {
            var cmd = DB.CreateCommand();
            cmd.CommandText = $"SELECT * from t_chapter WHERE owner='{owner}'";
            using (SQLiteDataReader reader = cmd.ExecuteReader()) {
                while (reader.Read()) {
                    yield return ChapterEntry.FromReader(reader);
                }
            }
        }

        public ChapterList GetChapterList(ulong owner) {
            return new ChapterList(owner, GetChapterEntries(owner).Select((c) => c.ToChapterInfo()));
        }

        public bool Add(ChapterEntry entry) {
            try {
                var cmd = DB.CreateCommand();
                cmd.CommandText = $"INSERT INTO t_chapter (owner,position,label,skip) VALUES('{entry.Owner}','{entry.Position}','{entry.Label??""}','{(entry.Skip?1:0)}')";
                return cmd.ExecuteNonQuery() == 1;
            } catch (Exception e) {
                return false;
            }
        }

        public int AddAll(IEnumerable<ChapterEntry> entries) {
            using (var txn = DB.BeginTransaction()) {
                var c = entries.Aggregate(0, (acc, e) => {
                    var inc = Add(e) ? 1 : 0;
                    return acc + inc;
                });
                txn.Commit();
                return c;
            }
        }

        public void UpdateByChapterList(ChapterList updated) {
            var current = GetChapterList(updated.Owner);

            var appended = updated.Values.Except(current.Values, PComp).Select((c) => ChapterEntry.Create(updated.Owner, c)).ToList();
            var deleted = current.Values.Except(updated.Values, PComp).Select((c) => ChapterEntry.Create(updated.Owner, c)).ToList();
            var modified = updated.Values.Where((c) => c.IsModified).Intersect(current.Values, PComp);

            using (var txn = DB.BeginTransaction()) {

                foreach (var m in modified) {
                    var entry = Get(current.Owner, m.Position);
                    if (entry != null) {
                        entry.Skip = m.Skip;
                        entry.Label = m.Label;
                    }
                }

                foreach (var a in appended) {
                    Add(a);
                }
                // 残念ながら、Autoincrementのprimary keyのせいで、DuplicateKeyExceptionが出るから、InsertAllOnSubmitは使えない。
                //Table.InsertAllOnSubmit(appended);
                // Table.DeleteAllOnSubmit(deleted);
                foreach (var d in deleted) {
                    Delete(d.Owner, d.Position);
                }
                txn.Commit();
            }
            updated.ResetModifiedFlag();
        }

        private class PositionComparator : IEqualityComparer<ChapterInfo> {
            public bool Equals(ChapterInfo x, ChapterInfo y) {
                return x.Position == y.Position;
            }

            public int GetHashCode(ChapterInfo obj) {
                return obj.Position.GetHashCode();
            }
        }
        private static PositionComparator PComp = new PositionComparator();

    }
}
