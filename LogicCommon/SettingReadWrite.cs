#nullable disable

using System;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace XMLManager
{
    public class SettingReadWrite
    {
        public static readonly string XMLfileName = Path.Combine("Setting", "setting.xml");
        public static readonly string DatabaseCommonConnectionSettingXMLfileName = Path.Combine("Setting", "databaseCommonConnectionSetting.xml");

        public static Object XMLReadInfo(string path, string className)
        {

            StringBuilder sb = new("XMLManager.");
            sb.Append(className);
            Type type = Type.GetType(sb.ToString());

            //オブジェクトの生成と型指定
            DataContractSerializer serializer = new DataContractSerializer(type);
            Object set;

            using (XmlReader reader = XmlReader.Create(path))
            {
                //XMLファイルから読み込み、逆シリアル化する
                set = serializer.ReadObject(reader);
            }
            return set;
        }

        public static void XMLWrite(Object set, string className, string fileName)
        {
            XmlWriterSettings settings = new();
            settings.Encoding = new System.Text.UTF8Encoding(false);

            StringBuilder sb = new("XMLManager.");
            sb.Append(className);
            Type type = Type.GetType(sb.ToString());

            DataContractSerializer ser = new(type);

            using (XmlTextWriter writer = new(fileName, System.Text.Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 4;

                ser.WriteObject(writer, set);
            }
        }
    }
}
