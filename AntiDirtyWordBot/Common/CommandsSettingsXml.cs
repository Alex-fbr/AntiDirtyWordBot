using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace AntiDirtyWordBot.Common
{
    /// <summary>
    /// Настройки для хранения команд бота
    /// </summary>
    [XmlRoot]
    [Serializable]
    public class CommandsSettingsXml
    {
        [XmlArray]
        [XmlArrayItem(typeof(BotCommandXml), ElementName = nameof(BotCommandXml))]
        public List<BotCommandXml> BotCommandList { get; set; }

        public CommandsSettingsXml()
        {
            BotCommandList = new List<BotCommandXml>();
        }

        [Serializable]
        public class BotCommandXml
        {
            [XmlAttribute]
            public CommandType Type { get; set; }

            [XmlAttribute]
            public string Code { get; set; }

            [XmlAttribute]
            public string Description { get; set; }

            [XmlArray]
            [XmlArrayItem(typeof(List<Keyboard>), ElementName = "KeyboardItem")]
            public List<List<Keyboard>> KeyboardButtonList { get; set; }
        }


        [Serializable]
        public class Keyboard
        {
            [XmlElement]
            public string DisplayToUser { get; set; }

            [XmlElement]
            public string CallbackData { get; set; }
        }
    }
}
