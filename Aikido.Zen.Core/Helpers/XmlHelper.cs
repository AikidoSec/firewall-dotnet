using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for processing XML data.
    /// </summary>
    public static class XmlHelper
    {
        /// <summary>
        /// Flattens an XML element into a dictionary with dot-notation keys.
        /// </summary>
        /// <param name="result">The dictionary to store flattened data.</param>
        /// <param name="element">The XML element to flatten.</param>
        /// <param name="prefix">The prefix for keys in the dictionary.</param>
        public static void FlattenXml(IDictionary<string, string> result, XmlElement element, string prefix)
        {
            string newPrefix = string.IsNullOrEmpty(prefix) ? element.Name : $"{prefix}.{element.Name}";
            var childElementGroups = element.ChildNodes
                .OfType<XmlElement>()
                .GroupBy(x => x.Name)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var group in childElementGroups)
            {
                for (int i = 0; i < group.Value.Count; i++)
                {
                    var childElement = group.Value[i];
                    string indexedPrefix = group.Value.Count > 1 ? $"{newPrefix}.{group.Key}[{i}]" : $"{newPrefix}.{group.Key}";
                    FlattenXml(result, childElement, indexedPrefix);
                }
            }

            foreach (XmlNode childNode in element.ChildNodes)
            {
                if (childNode is XmlText textNode)
                {
                    result[newPrefix] = textNode.Value.Trim();
                }
            }
        }

        /// <summary>
        /// Converts an XML element to its appropriate native object representation.
        /// </summary>
        /// <param name="element">The XML element to convert.</param>
        /// <returns>The converted object.</returns>
        public static object XmlToObject(XmlElement element)
        {
            if (!element.HasChildNodes || (element.ChildNodes.Count == 1 && element.FirstChild is XmlText))
            {
                return element.InnerText.Trim();
            }

            var dict = new Dictionary<string, object>();
            var childElementGroups = element.ChildNodes
                .OfType<XmlElement>()
                .GroupBy(x => x.Name)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var group in childElementGroups)
            {
                if (group.Value.Count > 1)
                {
                    dict[group.Key] = group.Value.Select(child => XmlToObject(child)).ToList();
                }
                else
                {
                    dict[group.Key] = XmlToObject(group.Value[0]);
                }
            }

            return dict;
        }
    }
}
