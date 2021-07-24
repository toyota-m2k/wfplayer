using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using static wfPlayer.WfFileItem;

namespace wfPlayer
{
    public class WfPlayListDB : IDisposable
    {
        public enum FieldFlag
        {
            MARK = 0x01,
            LAST_PLAY = 0x02,
            PLAY_COUNT = 0x04,
            RATING = 0x08,
            TRIMMING = 0x10,
            ASPECT = 0x20,
            TRIM_START = 0x40,
            TRIM_END = 0x80,
        }

        public class Txn : IDisposable
        {
            private SQLiteTransaction mTxn;

            internal Txn(SQLiteTransaction txn)
            {
                mTxn = txn;
            }

            public void Commit()
            {
                Dispose();
            }

            public void Rollback()
            {
                mTxn?.Rollback();
                mTxn?.Dispose();
                mTxn = null;
            }

            public void Dispose()
            {
                mTxn?.Commit();
                mTxn?.Dispose();
                mTxn = null;
            }

        }

        public class Appender : IDisposable
        {
            private SQLiteCommand mCmd;
            private Txn mTxn;
            internal Appender(SQLiteCommand cmd, Txn txn)
            {
                mCmd = cmd;
                mTxn = txn;
            }

            public void Dispose()
            {
                mCmd?.Dispose();
                mTxn?.Dispose();
                mCmd = null;
                mTxn = null;
            }

            public bool Add(WfFileItem f)
            {
                try
                {
                    long lastPlay = f.LastPlayDate == DateTime.MinValue ? 0 : f.LastPlayDate.ToFileTimeUtc();
                    mCmd.CommandText = $"INSERT INTO t_playlist (path,date,size,mark,rating,lastPlay,playCount,trimming,aspect) "
                                     + $"VALUES('{f.FullPath}','{f.Date.ToFileTimeUtc()}','{f.Size}','{f.Mark}','{(int)f.Rating}','{lastPlay}','{f.PlayCount}','0', '{(long)f.Aspect}')";
                    return 1 == mCmd.ExecuteNonQuery();
                }
                catch (SQLiteException)
                {
                    return false;
                }
            }

            public void Rollback()
            {
                mTxn.Rollback();
                Dispose();
            }
            public void Commit()
            {
                Dispose();
            }
        }

        public class Remover : IDisposable {
            private SQLiteCommand mCmd;
            private Txn mTxn;
            internal Remover(SQLiteCommand cmd, Txn txn) {
                mCmd = cmd;
                mTxn = txn;
            }

            public void Dispose() {
                mCmd?.Dispose();
                mTxn?.Dispose();
                mCmd = null;
                mTxn = null;
            }

            public bool Remove(string path) {
                try {
                    mCmd.CommandText = $"DELETE FROM t_playlist WHERE path='{path}'";
                    return 1 == mCmd.ExecuteNonQuery();
                } catch (SQLiteException) {
                    return false;
                }
            }

            public void Rollback() {
                mTxn.Rollback();
                Dispose();
            }

            public void Commit() {
                Dispose();
            }
        }

        public class Retriever : IDisposable // , IEnumerator<WfFileItem>, IEnumerable<WfFileItem>
        {
            SQLiteCommand mCmd;
            //SQLiteDataReader mReader;
            //bool mHasValue = true;
            //WfFileItem mValue;
            bool mUpdateExistsFlag;

            private bool ReadOne(SQLiteDataReader reader, out WfFileItem rec) {
                rec = null;
                if (!reader.Read()) {
                    return false;
                }
                var path = Convert.ToString(reader["path"]);
                bool exists = true;
                if (mUpdateExistsFlag) {
                    exists = File.Exists(path);
                }
                long lastPlay = Convert.ToInt64(reader["lastPlay"]);
                DateTime lastPlayDate = (lastPlay == 0) ? DateTime.MinValue : DateTime.FromFileTimeUtc(lastPlay);

                WfFileItem.Trim trim = WfFileItem.Trim.NoTrim;
                var rawTrimId = reader["trim_id"];
                long trimId = !(rawTrimId is DBNull) ? Convert.ToInt64(rawTrimId) : 0;
                //if (!(rawTrimId is DBNull)) {
                //    long trimId = Convert.ToInt64(rawTrimId);
                //    if (trimId > 0) {
                //        try {
                //            trim = new WfFileItem.Trim(trimId, Convert.ToString(reader["trim_name"]),
                //                Convert.ToInt64(reader["prologue"]),
                //                Convert.ToInt64(reader["epilogue"]),
                //                refPath: "");
                //        } catch (SQLiteException) {
                //            trim = WfFileItem.Trim.NoTrim;
                //        }
                //    }
                //}
                rec = new WfFileItem(
                                Convert.ToUInt64(reader["id"]),
                                path,
                                Convert.ToInt64(reader["size"]),
                                DateTime.FromFileTimeUtc(Convert.ToInt64(reader["date"])),
                                Convert.ToString(reader["mark"]),
                                (Ratings)Convert.ToInt32(reader["rating"]),
                                exists,
                                lastPlayDate,
                                Convert.ToInt32(reader["playCount"]),
                                trimId,
                                (WfAspect)Convert.ToInt32(reader["aspect"]),
                                Convert.ToUInt64(reader["trim_start"]),
                                Convert.ToUInt64(reader["trim_end"])
                                );
                return true;
            }

            internal Retriever(SQLiteCommand cmd, bool checkExists)
            {
                mCmd = cmd;
                mUpdateExistsFlag = checkExists;
            }
            public void Dispose()
            {
                mCmd.Dispose();
                mCmd = null;
            }

            public IEnumerable<WfFileItem> List {
                get {
                    using (var reader = mCmd.ExecuteReader()) {
                        WfFileItem result;
                        while (ReadOne(reader, out result)) {
                            yield return result;
                        }
                    }
                }
            }

            //public WfFileItem Current => mValue;

            //object IEnumerator.Current => mValue;


            //public bool MoveNext()
            //{
            //    ReadOne();
            //    return mHasValue;
            //}

            //public void Reset()
            //{
            //    Debug.WriteLine("WfPlayerListDB.Retriever not supprot Reset()");
            //}

            //public IEnumerator<WfFileItem> GetEnumerator()
            //{
            //    return this;
            //}

            //IEnumerator IEnumerable.GetEnumerator()
            //{
            //    return this;
            //}
        }

        public static WfPlayListDB Instance { get; private set; } = null;
        public static WfPlayListDB CreateInstance(string path)
        {
            if(Instance!=null)
            {
                Instance.Dispose();
            }
            Instance = new WfPlayListDB();
            Instance.Open(path);
            return Instance;
        }

        //public TrimmingPattern TP;

        private SQLiteConnection mDB;
        
        public ChapterTable ChapterTable { get; private set; }
        
        private WfPlayListDB() {
        }

        private void Open(string dbPath)
        {
            var builder = new SQLiteConnectionStringBuilder() { DataSource = dbPath ?? "wfplay.db" };
            mDB = new SQLiteConnection(builder.ToString());
            mDB.Open();
            executeSql(
                @"CREATE TABLE IF NOT EXISTS t_trim_patterns (
                    trim_id INTEGER NOT NULL PRIMARY KEY,
                    trim_name TEXT NOT NULL UNIQUE,
                    prologue INTEGER NOT NULL,
                    epilogue INTEGER NOT NULL,
                    ref_path TEXT
                    )",
                @"CREATE TABLE IF NOT EXISTS t_playlist (
                    id INTEGER NOT NULL PRIMARY KEY,
                    path TEXT NOT NULL UNIQUE,
                    date INTEGER NOT NULL,
                    size INTEGER NOT NULL,
                    mark TEXT,
                    rating INTEGER NOT NULL,
                    lastPlay INTEGER NOT NULL,
                    playCount INTEGER NOT NULL,
                    trimming INTEGER NOT NULL,
                    flag INTEGER DEFAULT '0',
                    aspect INTEGER NOT NULL DEFAULT '0'
                    )",
                @"CREATE TABLE IF NOT EXISTS t_chapter (
	                id	INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    owner INTEGER NOT NULL,
                    position  INTEGER NOT NULL,
                    label TEXT,
                    skip INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY(owner) REFERENCES t_playlist(id),
                    UNIQUE(owner,position)
                )",
                @"CREATE INDEX IF NOT EXISTS idx_path ON t_playlist(path)",
                @"CREATE INDEX IF NOT EXISTS idx_status ON t_playlist(rating)",
                @"CREATE INDEX IF NOT EXISTS idx_mark ON t_playlist(mark)",
                @"CREATE TABLE IF NOT EXISTS t_target_folders (
                    id INTEGER NOT NULL PRIMARY KEY,
                    path TEXT NOT NULL UNIQUE
                    )",
                @"CREATE INDEX IF NOT EXISTS idx_folder_path ON t_target_folders(path)",
                @"CREATE TABLE IF NOT EXISTS t_key_value (
                    key TEXT NOT NULL PRIMARY KEY,
                    value TEXT NOT NULL
                    )"
                );

            UpdateTable();
            ChapterTable = new ChapterTable(mDB);
        }

        const string KEY_APP_NAME = "AppName";
        const string KEY_VERSION = "Version";
        const string VAL_APP_NAME = "WfPlayer";
        const int VAL_VERSION_1 = 1;
        const int VAL_CURRENT_VERSION = 2;

        public int Version = 0;

        void UpdateTable() {
            Version = Convert.ToInt32(GetValueAt(KEY_VERSION)??"0");
            var name = GetValueAt(KEY_APP_NAME);
            if(Version < VAL_CURRENT_VERSION) {
                if(name==null) {
                    try {
                        safeExecuteSql(@"ALTER TABLE t_playlist ADD COLUMN trim_start INTEGER DEFAULT '0'");
                        safeExecuteSql(@"ALTER TABLE t_playlist ADD COLUMN trim_end INTEGER DEFAULT '0'");

                        var tp = new TrimmingPattern(mDB);
                        var list = QueryAll(false).List;
                        foreach (var c in list) {
                            if (c.TrimmingId != 0) {
                                var trim = tp.GetById(c.TrimmingId);
                                if (trim != null) {
                                    c.TrimStart = (ulong)trim.Prologue;
                                    c.TrimEnd = (ulong)trim.Epilogue;
                                    c.TrimmingId = 0;
                                    c.SaveModified();
                                }
                            }
                        }
                        SetValueAt(KEY_APP_NAME, VAL_APP_NAME);
                    }
                    catch (Exception e) {
                        Debug.WriteLine(e);
                    }
                    SetValueAt(KEY_VERSION, $"{VAL_CURRENT_VERSION}");
                    Version = VAL_CURRENT_VERSION;
                }
            }
        }

        static List<string> splitPath(string path)
        {
            path = Path.GetFullPath(path);
            var list = new List<string>();
            var root = Path.GetPathRoot(path)??"";
            while (null != path && path.Length > 0)
            {
                var p = Path.GetFileName(path);
                if (p == null)
                {
                    break;
                }
                else if (p.Length != 0)
                {
                    list.Add(p);
                }
                path = Path.GetDirectoryName(path);
            }
            list.Add(root);
            return list;
        }

        enum FC
        {
            SAME,
            DIFF,
            L_IS_PARENT,
            R_IS_PARENT,
        }
        static FC isSubFolder(string left, string right)
        {
            var L = splitPath(left);
            var R = splitPath(right);

            int i, j;
            for( i=L.Count-1, j=R.Count-1; i>=0 && j>=0; i--, j--)
            {
                if(L[i]!=R[j])
                {
                    return FC.DIFF;
                }
            }
            if(i==j)
            {
                return FC.SAME;
            }
            else if(i<j)
            {
                return FC.L_IS_PARENT;
            }
            else
            {
                return FC.R_IS_PARENT;
            }
        }

        public Appender BeginRegister(string folderPath)
        {
            var cmd = mDB.CreateCommand();
            cmd.CommandText = @"SELECT path FROM t_target_folders";
            var removing = new List<string>();
            bool sub = false;
            using (var reader = cmd.ExecuteReader())
            {
                while(reader.Read())
                {
                    var path = Convert.ToString(reader["path"]);
                    var c = isSubFolder(folderPath, path);
                    if(c==FC.L_IS_PARENT)
                    {
                        removing.Add(path);
                    }
                    else if(c==FC.R_IS_PARENT)
                    {
                        sub = true;
                    }
                }
            }

            var txn = Transaction();

            if (!sub)
            {
                try
                {
                    cmd.CommandText = $"INSERT INTO t_target_folders (path) VALUES('{folderPath}')";
                    cmd.ExecuteNonQuery();
                }
                catch (SQLiteException)
                {

                }
            }
            if(removing.Count>0)
            {
                foreach (var path in removing)
                {
                    try
                    {
                        cmd.CommandText = $"DELETE FROM t_target_folders WHERE path='{path}'";
                        cmd.ExecuteNonQuery();
                    }
                    catch (SQLiteException)
                    {

                    }
                }

            }
            return new Appender(cmd, txn);
        }

        public Remover BeginRemove() {
            var txn = Transaction();
            var cmd = mDB.CreateCommand();
            return new Remover(cmd, txn);
        }

        public Retriever QueryAll(bool updateExists, string filter="", string orderBy="")
        {
            var cmd = mDB.CreateCommand();
            cmd.CommandText = $"SELECT * FROM t_playlist LEFT OUTER JOIN t_trim_patterns on t_playlist.trimming=t_trim_patterns.trim_id {filter} {orderBy}";
            return new Retriever(cmd, updateExists);
        }

        public IEnumerable<string> ListTargetFolders() {
            using (var cmd = mDB.CreateCommand()) {
                cmd.CommandText = $"SELECT * from t_target_folders";
                using (SQLiteDataReader reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        yield return Convert.ToString(reader["path"]);
                    }
                }
            }
        }

        public void UpdatePlaylistItem(WfFileItem item, long flags)
        {
            if(flags==0)
            {
                return;
            }
            using (var cmd = mDB.CreateCommand()) {
                var sql = new StringBuilder("UPDATE t_playlist SET ");
                bool prev = false;
                if (0 != (flags & (long)FieldFlag.MARK)) {
                    sql.Append($"mark='{item.Mark}' "); prev = true;
                }
                if (0 != (flags & (long)FieldFlag.RATING)) {
                    if (prev) sql.Append(", ");
                    sql.Append($"rating='{(int)item.Rating}' "); prev = true;
                }
                if (0 != (flags & (long)FieldFlag.PLAY_COUNT)) {
                    if (prev) sql.Append(", ");
                    sql.Append($"playCount='{item.PlayCount} '"); prev = true;
                }
                if (0 != (flags & (long)FieldFlag.LAST_PLAY)) {
                    long tm = item.LastPlayDate == DateTime.MinValue ? 0 : item.LastPlayDate.ToFileTimeUtc();
                    if (prev) sql.Append(", ");
                    sql.Append($"lastPlay='{tm}' "); prev = true;
                }
                //if (0 != (flags & (long)FieldFlag.TRIMMING))
                //{
                //    if (prev) sql.Append(", ");
                //    sql.Append($"trimming='{item.Trimming.Id}' "); prev = true;
                //}
                if (0 != (flags & (long)FieldFlag.ASPECT)) {
                    if (prev) sql.Append(", ");
                    sql.Append($"aspect='{(long)item.Aspect}' "); prev = true;
                }
                if (0 != (flags & (long)FieldFlag.TRIM_START)) {
                    if (prev) sql.Append(", ");
                    sql.Append($"trim_start='{item.TrimStart}' "); prev = true;
                }
                if (0 != (flags & (long)FieldFlag.TRIM_END)) {
                    if (prev) sql.Append(", ");
                    sql.Append($"trim_end='{item.TrimEnd}' "); prev = true;
                }
                sql.Append($" WHERE path='{item.FullPath}'");
                executeSql(sql.ToString());
            }
        }

        public bool SetValueAt(string key, string value)
        {
            using (var cmd = mDB.CreateCommand())
            {
                try
                {
                    cmd.CommandText = $"UPDATE t_key_value SET value='{value}' WHERE key='{key}'";
                    if (1 == cmd.ExecuteNonQuery())
                    {
                        return true;
                    }
                    cmd.CommandText = $"INSERT INTO t_key_value (key,value) VALUES('{key}','{value}')";
                    return 1 == cmd.ExecuteNonQuery();
                }
                catch (SQLiteException)
                {
                    return false;
                }
            }
        }

        public string GetValueAt(string key)
        {
            using (var cmd = mDB.CreateCommand())
            {
                cmd.CommandText = $"SELECT value FROM t_key_value WHERE key='{key}'";
                using(var reader = cmd.ExecuteReader())
                {
                    if(reader.Read())
                    {
                        return Convert.ToString(reader["value"]);
                    }
                }
            }
            return null;
        }

        public Txn Transaction()
        {
            return new Txn(mDB.BeginTransaction());
        }

        private bool safeExecuteSql(params string[] sqls) {
            try {
                executeSql(sqls);
                return true;
            } catch(Exception e) {
                Debug.WriteLine(e);
                return false;
            }
        }


        private void executeSql(params string[] sqls)
        {
            using (var cmd= mDB.CreateCommand())
            {
                foreach (var sql in sqls)
                {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void Dispose()
        {
            mDB?.Close();
            mDB?.Dispose();
            mDB = null;
        }

        public class TrimmingPattern
        {
            WeakReference<SQLiteConnection> mRefDB;
            public TrimmingPattern(SQLiteConnection db)
            {
                mRefDB = new WeakReference<SQLiteConnection>(db);
            }
            SQLiteConnection DB
            {
                get
                {
                    SQLiteConnection db = null;
                    return (mRefDB?.TryGetTarget(out db) ?? false) ? db : null;
                }
            }

            public ITrim Register(ITrim trim, long id=0)
            {
                return Register(id, trim.Name, trim.Prologue, trim.Epilogue, trim.RefPath);
            }
            public ITrim Register(long id, string name, double prologue, double epilogue, string refPath)
            {
                if(String.IsNullOrEmpty(name)||(prologue==0&&epilogue==0))
                {
                    return null;
                }

                using (var cmd = DB.CreateCommand())
                {
                    try
                    {
                        if(id==0)
                        {
                            // register
                            cmd.CommandText = $"INSERT INTO t_trim_patterns (trim_name,prologue,epilogue,ref_path) VALUES('{name}','{(long)prologue}','{(long)epilogue}','{refPath}' )";
                        }
                        else
                        {
                            // update
                            cmd.CommandText = $"UPDATE t_trim_patterns SET trim_name = '{name}', prologue='{(long)prologue}', epilogue = '{(long)epilogue}', ref_path='{refPath}' WHERE trim_id='{id}'";
                        }
                        if(1 == cmd.ExecuteNonQuery())
                        {
                            return Get(name);
                        }
                    }
                    catch (SQLiteException)
                    {
                    }
                    return null;
                }

            }

            public bool Remove(string name)
            {
                using (var cmd = DB.CreateCommand())
                {
                    try
                    {

                        cmd.CommandText = $"DELETE FROM t_trim_patterns WHERE trim_name='{name}'";
                        return 1 == cmd.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }

            public ITrim Get(string name)
            {
                using (var cmd = DB.CreateCommand())
                {
                    try
                    {

                        cmd.CommandText = $"SELECT * FROM t_trim_patterns WHERE trim_name='{name}'";
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new WfFileItem.Trim(Convert.ToInt64(reader["trim_id"]),
                                    Convert.ToString(reader["trim_name"]),
                                    Convert.ToInt64(reader["prologue"]),
                                    Convert.ToInt64(reader["epilogue"]),
                                    Convert.ToString(reader["ref_path"]));
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                    return null;
                }
            }

            public ITrim GetById(long id) {
                using (var cmd = DB.CreateCommand()) {
                    try {

                        cmd.CommandText = $"SELECT * FROM t_trim_patterns WHERE trim_id='{id}'";
                        using (var reader = cmd.ExecuteReader()) {
                            if (reader.Read()) {
                                return new WfFileItem.Trim(Convert.ToInt64(reader["trim_id"]),
                                    Convert.ToString(reader["trim_name"]),
                                    Convert.ToInt64(reader["prologue"]),
                                    Convert.ToInt64(reader["epilogue"]),
                                    Convert.ToString(reader["ref_path"]));
                            }
                        }
                    }
                    catch (Exception) {
                    }
                    return null;
                }
            }

            public int List(ICollection<WfFileItem.Trim> list)
            {
                using (var cmd = DB.CreateCommand())
                {
                    try
                    {
                        cmd.CommandText = $"SELECT * FROM t_trim_patterns ORDER BY trim_name";
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                list.Add(new WfFileItem.Trim(Convert.ToInt64(reader["trim_id"]),
                                    Convert.ToString(reader["trim_name"]),
                                    Convert.ToInt64(reader["prologue"]),
                                    Convert.ToInt64(reader["epilogue"]),
                                    Convert.ToString(reader["ref_path"])));
                            }
                            return list.Count;
                        }
                    }
                    catch (Exception)
                    {
                    }
                    return -1;
                }
            }
        }
    
    }
}
