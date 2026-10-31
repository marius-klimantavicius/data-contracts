using System.Xml;
using System.Xml.Linq;

namespace Marius.DataContracts.Playground;

// Comparison for the showcase's DataContract XML. Attribute order and namespace
// prefixes are irrelevant; character content (including spaces) is significant.
internal static class XmlEquivalence
{
    public static bool AreEqual(string left, string right) => AreEqual(
        XElement.Parse(left, LoadOptions.PreserveWhitespace),
        XElement.Parse(right, LoadOptions.PreserveWhitespace));

    public static bool AreEqual(XElement left, XElement right)
    {
        if (left.Name != right.Name)
            return false;

        var attributes = left.Attributes().Where(a => !a.IsNamespaceDeclaration).ToArray();
        if (attributes.Length != right.Attributes().Count(a => !a.IsNamespaceDeclaration))
            return false;

        foreach (var attribute in attributes)
        {
            var other = right.Attribute(attribute.Name);
            if (other is null) return false;

            if (attribute.Name == XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance"))
            {
                if (ExpandName(left, attribute.Value) != ExpandName(right, other.Value))
                    return false;
            }
            else if (attribute.Value != other.Value)
            {
                return false;
            }
        }

        var leftContent = Content(left).ToArray();
        var rightContent = Content(right).ToArray();
        return leftContent.Length == rightContent.Length && leftContent.Zip(rightContent).All(pair => pair.First is XElement a && pair.Second is XElement b ? AreEqual(a, b) : pair.First is string x && pair.Second is string y && x == y);
    }

    private static XName ExpandName(XElement element, string value)
    {
        var parts = value.Trim().Split(':');
        if (parts.Length == 1)
            return element.GetDefaultNamespace() + parts[0];

        if (parts.Length == 2 && element.GetNamespaceOfPrefix(parts[0]) is { } ns)
            return ns + parts[1];

        throw new XmlException($"Invalid or unbound QName: {value}");
    }

    private static IEnumerable<object> Content(XElement element)
    {
        var text = new System.Text.StringBuilder();
        foreach (var node in element.Nodes())
        {
            if (node is XText characters)
            {
                text.Append(characters.Value);
            }
            else if (node is XElement child)
            {
                if (text.Length != 0)
                {
                    yield return text.ToString();

                    text.Clear();
                }

                yield return child;
            }
        }

        if (text.Length != 0)
            yield return text.ToString();
    }
}