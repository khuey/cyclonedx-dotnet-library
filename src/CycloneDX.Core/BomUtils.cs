// This file is part of CycloneDX Library for .NET
//
// Licensed under the Apache License, Version 2.0 (the “License”);
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an “AS IS” BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) OWASP Foundation. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CycloneDX.Models;

namespace CycloneDX
{
    public static class BomUtils
    {
        internal static DateTime? UtcifyDateTime(DateTime? value)
        {
            if (value is null)
            {
                return null;
            }
            else if (value.Value.Kind == DateTimeKind.Unspecified)
            {
                return DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
            }
            else if (value.Value.Kind == DateTimeKind.Local)
            {
                return value.Value.ToUniversalTime();
            }
            else
            {
                return value;
            }
        }

        internal static Bom GetBomForSerialization(Bom bom)
        {
            if (bom.SpecVersion == SpecificationVersionHelpers.CurrentVersion)
            {
                return bom;
            }
            else
            {
                var downgradedBom = CopyBomAndDowngrade(bom);
                return downgradedBom;
            }
        }

        internal static Bom CopyBomAndDowngrade(Bom bom)
        {
            var bomCopy = bom.Copy();

            // we downgrade stuff starting with lowest spec first
            // this will remove entire classes of things and will save unnecessary processing further down
            if (bomCopy.SpecVersion < SpecificationVersion.v1_1)
            {
                bomCopy.SerialNumber = null;
                bomCopy.ExternalReferences = null;

                EnumerateAllComponents(bomCopy, (component) => {
                    component.BomRef = null;
                    component.Pedigree = null;
                    component.ExternalReferences = null;
                });
            }

            if (bomCopy.SpecVersion < SpecificationVersion.v1_2)
            {
                bomCopy.Metadata = null;
                bomCopy.Dependencies = null;
                bomCopy.Services = null;

                EnumerateAllComponents(bomCopy, (component) => {
                    component.Author = null;
                    component.MimeType = null;
                    component.Supplier = null;
                    component.Swid = null;

                    if (component.Pedigree != null)
                    {
                        component.Pedigree.Patches = null;
                    }
                });
            }

            if (bomCopy.SpecVersion < SpecificationVersion.v1_3)
            {
                bomCopy.Compositions = null;
                if (bomCopy.Metadata != null)
                {
                    bomCopy.Metadata.Licenses = null;
                    bomCopy.Metadata.Properties = null;
                }
                EnumerateAllComponents(bomCopy, (component) => {
                    component.Properties = null;
                    component.Evidence = null;
                    if (component.ExternalReferences != null)
                    {
                        foreach (var extRef in component.ExternalReferences)
                        {
                            extRef.Hashes = null;
                        }
                    }
                });
                EnumerateAllServices(bomCopy, (service) => {
                    service.Properties = null;
                    if (service.ExternalReferences != null)
                    {
                        foreach (var extRef in service.ExternalReferences)
                        {
                            extRef.Hashes = null;
                        }
                    }
                });
            }

            if (bomCopy.SpecVersion < SpecificationVersion.v1_4)
            {
                if (bomCopy.Metadata?.Tools != null)
                {
                    foreach (var tool in bomCopy.Metadata.Tools)
                    {
                        tool.ExternalReferences = null;
                    }
                }
                EnumerateAllComponents(bomCopy, (component) => {
                    component.ReleaseNotes = null;
                    if (component.Version == null)
                    {
                        component.Version = "0.0.0";
                    }
                });
                EnumerateAllServices(bomCopy, (service) => {
                    service.ReleaseNotes = null;
                });
                bomCopy.Vulnerabilities = null;
            }

            return bomCopy;
        }

        public static Bom Copy(this Bom bom)
        {
            var protoBom = Protobuf.Serializer.SerializeForDeepCopy(bom);
            var bomCopy = Protobuf.Serializer.Deserialize(protoBom);
            return bomCopy;
        }

        public static void EnumerateAllComponents(Bom bom, Action<Component> callback)
        {
            var q = new Queue<Component>();

            if (bom.Metadata?.Component != null)
            {
                q.Enqueue(bom.Metadata.Component);
            }

            if (bom.Components != null)
            {
                foreach (var component in bom.Components)
                {
                    q.Enqueue(component);
                }
            }

            while (q.Count > 0)
            {
                var currentComponent = q.Dequeue();
                
                callback(currentComponent);

                if (currentComponent.Components != null)
                {
                    foreach (var c in currentComponent.Components)
                    {
                        q.Enqueue(c);
                    }
                }
                if (currentComponent.Pedigree?.Ancestors != null)
                {
                    foreach (var c in currentComponent.Pedigree.Ancestors)
                    {
                        q.Enqueue(c);
                    }
                }
                if (currentComponent.Pedigree?.Descendants != null)
                {
                    foreach (var c in currentComponent.Pedigree.Descendants)
                    {
                        q.Enqueue(c);
                    }
                }
                if (currentComponent.Pedigree?.Variants != null)
                {
                    foreach (var c in currentComponent.Pedigree.Variants)
                    {
                        q.Enqueue(c);
                    }
                }
            }
        }

        public static void EnumerateAllServices(Bom bom, Action<Service> callback)
        {
            var q = new Queue<Service>();

            if (bom.Services != null)
            {
                foreach (var service in bom.Services)
                {
                    q.Enqueue(service);
                }
            }

            while (q.Count > 0)
            {
                var currentService = q.Dequeue();
                
                callback(currentService);

                if (currentService.Services != null)
                {
                    foreach (var s in currentService.Services)
                    {
                        q.Enqueue(s);
                    }
                }
            }
        }
    }
}