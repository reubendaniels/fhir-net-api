﻿/* 
 * Copyright (c) 2014, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/ewoutkramer/fhir-net-api/master/LICENSE
 */

using Hl7.Fhir.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Hl7.Fhir.Support;
using Hl7.Fhir.Model;
using Hl7.Fhir.Introspection;
using System.Diagnostics;

namespace Hl7.Fhir.Specification.Source
{
    /// <summary>
    /// Internal class which is able to scan a (possibly) large Xml conformance resource for its identifying url
    /// </summary>
    internal class ConformanceArtifactScanner
    {
        public static bool IsConformanceResource(string name)
        {
            return name == "Conformance" || name == "StructureDefinition" || name == "ValueSet" || name == "ConceptMap" ||
                name == "DataElement" || name == "OperationDefinition" || name == "SearchParameter" || name == "NamingSystem";
        }
     
        private Stream _input;
        private string _origin;

        public ConformanceArtifactScanner(Stream input, string origin)
        {
            _input = input;
            _origin = origin;
        }

        /// <summary>
        /// Scan a supplied (bundle or single resource) file with the core artifacts for an entry with an id equal to the given uri 
        /// </summary>
        /// <param name="identifier">identifying uri of the conformance resource to find</param>
        /// <returns></returns>
        public XElement FindConformanceResourceById(string identifier)
        {
            if (identifier == null) throw Error.ArgumentNull("identifier");

            var resources = StreamResources();

            return resources.Where(res => IsConformanceResource(res.Name.LocalName) &&
                getPrimitiveValueElement(res,"url") == identifier).SingleOrDefault();
        }

        private string getPrimitiveValueElement(XElement element, string name)
        {
            var valueElem = element.Element(XmlNs.XFHIR + name);
            if(valueElem == null) return null;

            var valueAttr = valueElem.Attribute("value");

            return valueAttr != null ? valueAttr.Value : null;
        }

        private Lazy<EnumMapping> _resourceTypeMapping = new Lazy<EnumMapping>(() => EnumMapping.Create(typeof(ResourceType)));

        private ResourceType stringNameToEnum(string name)
        {
            return (ResourceType)_resourceTypeMapping.Value.ParseLiteral(name);
        }

        public IEnumerable<ConformanceInformation> ListConformanceResourceInformation()
        {
            var resources = StreamResources();

            return resources.Where(res => IsConformanceResource(res.Name.LocalName))
                .Select(res =>
                        new ConformanceInformation()
                        {
                            Url = getPrimitiveValueElement(res, "url"),
                            Name = getPrimitiveValueElement(res, "name"),
                            Origin = _origin,
                            Type = stringNameToEnum(res.Name.LocalName)
                        });                       
        }

        private static string getRootName(XmlReader reader)
        {
           while (reader.Read())
           {
               if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == XmlNs.FHIR)
                   return reader.LocalName;
           }

           return null;
        }

        // Use a forward-only XmlReader to scan through a possibly huge bundled file,
        // and yield the feed entries, so only one entry is in memory at a time
        internal IEnumerable<XElement> StreamResources()
        {
            var reader = FhirParser.XmlReaderFromStream(_input);

            var root = getRootName(reader);

            if (root == "Bundle")
            {
                if (!reader.ReadToDescendant("entry", XmlNs.FHIR)) yield break;

                while (!reader.EOF)
                {
                    if (reader.NodeType == XmlNodeType.Element
                        && reader.NamespaceURI == XmlNs.FHIR && reader.LocalName == "entry")
                    {
                        var entryNode = (XElement)XElement.ReadFrom(reader);
                        yield return entryNode.Elements().First().Elements().First();
                    }
                    else
                        reader.Read();
                }
            }
            else if (root != null)
            {
                var resourceNode = (XElement)XElement.ReadFrom(reader);
                yield return resourceNode;
            }
            else
                yield break;
        }      
    }
}
