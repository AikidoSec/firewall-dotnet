using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for processing XML data.
    /// </summary>
    internal static class XmlHelper
    {
        internal const int MaxDepth = 64;

        /// <summary>
        /// Flattens an XML element into a dictionary with dot-notation keys.
        /// </summary>
        /// <param name="result">The dictionary to store flattened data.</param>
        /// <param name="element">The XML element to flatten.</param>
        /// <param name="prefix">The prefix for keys in the dictionary.</param>
        internal static void FlattenXml(IDictionary<string, string> result, XmlElement element, string prefix, int depth = 0)
        {
            if (depth >= MaxDepth)
            {
                return;
            }

            // Determine the new prefix based on whether the current element is the root or a child
            string newPrefix = string.IsNullOrEmpty(prefix) ? string.Empty : prefix;

            var childElementGroups = element.ChildNodes
                    .OfType<XmlElement>()
                .GroupBy(x => x.Name)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var group in childElementGroups)
            {
                for (int i = 0; i < group.Value.Count; i++)
                {
                    var childElement = group.Value[i];
                    // Use indexed prefix only if there are multiple children with the same name
                    string indexedPrefix = group.Value.Count > 1 ? $"{newPrefix}.{group.Key}.{i}" : $"{newPrefix}.{group.Key}";
                    FlattenXml(result, childElement, indexedPrefix, depth + 1);
                }
            }

            StringBuilder text = null;
            foreach (XmlNode childNode in element.ChildNodes)
            {
                if (childNode is XmlText || childNode is XmlCDataSection)
                {
                    text = text ?? new StringBuilder();
                    text.Append(childNode.Value);
                }
            }

            if (text != null)
            {
                // Text and CDATA can split one input, so inspect their combined value.
                result[string.IsNullOrEmpty(newPrefix) ? element.Name : newPrefix] = text.ToString().Trim();
            }
        }

        /// <summary>
        /// Converts an XML element to its appropriate native object representation.
        /// </summary>
        /// <param name="element">The XML element to convert.</param>
        /// <returns>The converted object.</returns>
        internal static object XmlToObject(XmlElement element, int depth = 0)
        {
            if (depth >= MaxDepth)
            {
                return new Dictionary<string, object>();
            }

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
                    dict[group.Key] = group.Value.Select(child => XmlToObject(child, depth + 1)).ToList();
                }
                else
                {
                    dict[group.Key] = XmlToObject(group.Value[0], depth + 1);
                }
            }

            return dict;
        }
    }
}
