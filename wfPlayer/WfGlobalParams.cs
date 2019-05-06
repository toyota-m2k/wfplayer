using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wfPlayer
{
    public class WfGlobalParams
    {
        public WinPlacement Placement { get; set; } = new WinPlacement();

        public string FilePath { get; set; } = "default.wfp";

        public WfFilter Filter { get; set; } = new WfFilter();

        public List<string> MRU { get; set; } = new List<string>(12);

        public void AddMru(string path)
        {
            if (null == path)
            {
                return;
            }
            MRU.Remove(path);
            MRU.Insert(0, path);
            while (MRU.Count > 10)
            {
                MRU.RemoveAt(10);
            }
        }

        public bool HasMru
        {
            get
            {
                return MRU.Count > 0;
            }
        }


        private static readonly string SETTINGS_FILE = "wfplayer.info";


        public static WfGlobalParams sInstance = null;
        public static WfGlobalParams Instance
        {
            get
            {
                if(sInstance==null)
                {
                    sInstance = Deserialize();
                }
                return sInstance;
            }
        }

        public void Serialize()
        {
            System.IO.StreamWriter sw = null;
            try
            {
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(WfGlobalParams));
                //書き込むファイルを開く（UTF-8 BOM無し）
                sw = new System.IO.StreamWriter(SETTINGS_FILE, false, new System.Text.UTF8Encoding(false));
                //シリアル化し、XMLファイルに保存する
                serializer.Serialize(sw, this);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
            finally
            {
                //ファイルを閉じる
                if (null != sw)
                {
                    sw.Close();
                }
            }
        }

        public static WfGlobalParams Deserialize()
        {
            System.IO.StreamReader sr = null;
            Object obj = null;

            try
            {
                //XmlSerializerオブジェクトを作成
                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(WfGlobalParams));

                //読み込むファイルを開く
                sr = new System.IO.StreamReader(SETTINGS_FILE, new System.Text.UTF8Encoding(false));

                //XMLファイルから読み込み、逆シリアル化する
                obj = serializer.Deserialize(sr);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                obj = new WfGlobalParams();
            }
            finally
            {
                if (null != sr)
                {
                    //ファイルを閉じる
                    sr.Close();
                }
            }
            return (WfGlobalParams)obj;
        }

    }
}
