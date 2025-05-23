using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol;
using ModelContextProtocol.Handlers;
using ModelContextProtocol.Models;
using System;
using System.Collections.Generic;
using ViewAppxPackage;

namespace TestProject1
{
    [TestClass]
    public class McpTests
    {
        [TestMethod]
        public void TestMcpJsonFormat()
        {
            // Create a sample package data
            var packageItems = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    { "name", "TestPackage" },
                    { "fullName", "TestPackage_1.0.0.0_x64__abc123" },
                    { "familyName", "TestPackage_abc123" },
                    { "publisher", "TestPublisher" },
                    { "publisherDisplayName", "Test Publisher" },
                    { "version", "1.0.0.0" },
                    { "installedDate", DateTime.Now },
                    { "isFramework", false },
                    { "isResourcePackage", false },
                    { "isBundle", false },
                    { "isDevelopmentMode", true },
                    { "installedPath", "C:\\Test\\Path" }
                }
            };
            
            // Create a model context with the items
            var modelContext = new ModelContext
            {
                Items = packageItems
            };
            
            // Use MCP to serialize
            var jsonHandler = new JsonModelContextHandler();
            var json = jsonHandler.GetFormattedResponse(modelContext);
            
            // Verify JSON structure is as expected
            Assert.IsTrue(json.Contains("\"name\""));
            Assert.IsTrue(json.Contains("\"fullName\""));
            Assert.IsTrue(json.Contains("\"familyName\""));
            Assert.IsTrue(json.Contains("\"publisher\""));
            Assert.IsTrue(json.Contains("\"publisherDisplayName\""));
            Assert.IsTrue(json.Contains("\"version\""));
            Assert.IsTrue(json.Contains("\"installedDate\""));
            Assert.IsTrue(json.Contains("\"isFramework\""));
            Assert.IsTrue(json.Contains("\"isBundle\""));
            Assert.IsTrue(json.Contains("\"installedPath\""));
            
            // Verify values
            Assert.IsTrue(json.Contains("TestPackage"));
            Assert.IsTrue(json.Contains("TestPublisher"));
            Assert.IsTrue(json.Contains("1.0.0.0"));
        }
    }
}