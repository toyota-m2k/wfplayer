﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                    mCmd.CommandText = $"INSERT INTO t_playlist (path,date,size,mark,rating,lastPlay,playCount,trimming) "
                                     + $"VALUES('{f.FullPath}','{f.Date.ToFileTimeUtc()}','{f.Size}','{f.Mark}','{(int)f.Rating}','{lastPlay}','{f.PlayCount}','{f.Trimming.Id}')";
                    return 1 == mCmd.ExecuteNonQuery();
                }
                catch (SQLiteException e)
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

        public class Retriever : IDisposable, IEnumerator<WfFileItem>, IEnumerable<WfFileItem>
        {
            SQLiteDataReader mReader;
            bool mHasValue;
            WfFileItem mValue;
            bool mUpdateExistsFlag;

            internal void ReadOne()
            {
                mHasValue = mReader.Read();
                if(mHasValue)
                {
                    var path = Convert.ToString(mReader["path"]);
                    bool exists = true;
                    if(mUpdateExistsFlag)
                    {
                        exists = File.Exists(path);
                    }
                    long lastPlay = Convert.ToInt64(mReader["lastPlay"]);
                    DateTime lastPlayDate = (lastPlay == 0) ? DateTime.MinValue : DateTime.FromFileTimeUtc(lastPlay);

                    WfFileItem.Trim trim = WfFileItem.Trim.NoTrim;
                    var rawTrimId = mReader["trim_id"];
                    if (!(rawTrimId is DBNull))
                    {
                        long trimId = Convert.ToInt64(rawTrimId);
                        if (trimId > 0)
                        {
                            try
                            {
                                trim = new WfFileItem.Trim(trimId, Convert.ToString(mReader["trim_name"]),
                                    TimeSpan.FromMilliseconds(Convert.ToInt64(mReader["prologue"])), TimeSpan.FromMilliseconds(Convert.ToInt64(mReader["epilogue"])));
                            }
                            catch (SQLiteException e)
                            {
                                trim = WfFileItem.Trim.NoTrim;
                            }
                        }
                    }

                    mValue = new WfFileItem(
                                    path,
                                    Convert.ToInt64(mReader["size"]),
                                    DateTime.FromFileTimeUtc(Convert.ToInt64(mReader["date"])),
                                    Convert.ToString(mReader["mark"]),
                                    (WfFileItem.Ratings)Convert.ToInt32(mReader["rating"]), exists,
                                    lastPlayDate,
                                    Convert.ToInt32(mReader["playCount"]),
                                    trim
                                    );
                }
            }

            internal Retriever(SQLiteDataReader reader, bool checkExists)
            {
                mUpdateExistsFlag = checkExists;
                mReader = reader;
                ReadOne();
            }
            public void Dispose()
            {
                mReader?.Close();
                mReader = null;
            }

            public WfFileItem Current => mValue;

            object IEnumerator.Current => mValue;


            public bool MoveNext()
            {
                ReadOne();
                return mHasValue;
            }

            public void Reset()
            {
                Debug.WriteLine("WfPlayerListDB.Retriever not supprot Reset()");
            }

            public IEnumerator<WfFileItem> GetEnumerator()
            {
                return this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }
        }

        private SQLiteConnection mDB;
        public WfPlayListDB(string dbPath)
        {
            var builder = new SQLiteConnectionStringBuilder() { DataSource = dbPath ?? "wfplay.db" };
            mDB = new SQLiteConnection(builder.ToString());
            mDB.Open();
            executeSql(
                @"CREATE TABLE IF NOT EXISTS t_trim_patterns (
                    trim_id INTEGER NOT NULL PRIMARY KEY,
                    trim_name TEXT NOT NULL UNIQUE,
                    prologue INTEGER NOT NULL,
                    epilogue INTEGER NOT NULL
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
                    flag INTEGER DEFAULT 0
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
                value TEXT NOT NULL UNIQUE
                )"
                );
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
                catch (SQLiteException e)
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
                    catch (SQLiteException e)
                    {

                    }
                }

            }
            return new Appender(cmd, txn);
        }

        public Retriever QueryAll(bool updateExists)
        {
            var cmd = mDB.CreateCommand();
            cmd.CommandText = @"SELECT * FROM t_playlist LEFT OUTER JOIN t_trim_patterns on t_playlist.trimming=t_trim_patterns.trim_id";
            return new Retriever(cmd.ExecuteReader(), updateExists);
        }

        public void UpdatePlaylistItem(WfFileItem item, long flags)
        {
            if(flags==0)
            {
                return;
            }
            using (var cmd = mDB.CreateCommand())
            {
                var sql = new StringBuilder("UPDATE t_playlist SET ");
                bool prev = false;
                if(0!=(flags & (long)FieldFlag.MARK))
                {
                    sql.Append($"mark='{item.Mark}' "); prev = true;
                }
                if(0!=(flags&(long)FieldFlag.RATING))
                {
                    if (prev) sql.Append(", ");
                    sql.Append($"rating='{(int)item.Rating}'");
                }
                if(0!=(flags&(long)FieldFlag.PLAY_COUNT))
                {
                    if (prev) sql.Append(", ");
                    sql.Append($"playCount='{item.PlayCount}'");
                }
                if (0 != (flags & (long)FieldFlag.LAST_PLAY))
                {
                    long tm = item.LastPlayDate == DateTime.MinValue ? 0 : item.LastPlayDate.ToFileTimeUtc();
                    if (prev) sql.Append(", ");
                    sql.Append($"lastPlay='{tm}'");
                }
                if (0 != (flags & (long)FieldFlag.TRIMMING))
                {
                    if (prev) sql.Append(", ");
                    sql.Append($"trimming='{item.Trimming.Id}'");
                }
                sql.Append($"WHERE path=${item.FullPath}");
                executeSql(sql.ToString());
            }
        }

        public bool SetValueAt(string key, string value)
        {
            using (var cmd = mDB.CreateCommand())
            {
                try
                {
                    cmd.CommandText = $"INSERT INTO t_key_value (key,value) VALUES('{key}','{value}')";
                    if (1 == cmd.ExecuteNonQuery())
                    {
                        return true;
                    }
                }
                catch (SQLiteException e)
                {

                }
                cmd.CommandText = $"UPDATE t_key_value SET value='{value}' WHERE key='{key}'";
                return 1 == cmd.ExecuteNonQuery();
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
    }
}
