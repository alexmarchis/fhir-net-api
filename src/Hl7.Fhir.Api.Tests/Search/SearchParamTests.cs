﻿using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Hl7.Fhir.Search;

namespace Hl7.Fhir.Tests
{
    [TestClass]
    public class SearchParamTests
    {
        [TestMethod]
        public void ParseCriterium()
        {
            var crit = Criterium.Parse("paramX", "18");
            Assert.AreEqual("paramX", crit.ParamName);
            Assert.IsNull(crit.Modifier);
            Assert.AreEqual("18", crit.Operand.ToString());
            Assert.AreEqual(Operator.EQ, crit.Type);

            crit = Criterium.Parse("paramX", ">18");
            Assert.AreEqual("paramX", crit.ParamName);
            Assert.IsNull(crit.Modifier);
            Assert.AreEqual("18", crit.Operand.ToString());
            Assert.AreEqual(Operator.GT, crit.Type);

            crit = Criterium.Parse("paramX:modif1", "~18");
            Assert.AreEqual("paramX", crit.ParamName);
            Assert.AreEqual("18", crit.Operand.ToString());
            Assert.AreEqual("modif1", crit.Modifier);
            Assert.AreEqual(Operator.APPROX, crit.Type);

            crit = Criterium.Parse("paramX:missing", "true");
            Assert.AreEqual("paramX", crit.ParamName);
            Assert.IsNull(crit.Operand);
            Assert.IsNull(crit.Modifier);
            Assert.AreEqual(Operator.ISNULL, crit.Type);

            crit = Criterium.Parse("paramX:missing", "false");
            Assert.AreEqual("paramX", crit.ParamName);
            Assert.IsNull(crit.Operand);
            Assert.IsNull(crit.Modifier);
            Assert.AreEqual(Operator.NOTNULL, crit.Type);
        }


        [TestMethod]
        public void ParseChain()
        {
            var crit = Criterium.Parse("par1:type1.par2.par3:text", "hoi");
            Assert.IsTrue(crit.Type == Operator.CHAIN);
            Assert.AreEqual("type1", crit.Modifier);
            Assert.IsTrue(crit.Operand is Criterium);

            crit = crit.Operand as Criterium;
            Assert.IsTrue(crit.Type == Operator.CHAIN);
            Assert.AreEqual(null, crit.Modifier);
            Assert.IsTrue(crit.Operand is Criterium);

            crit = crit.Operand as Criterium;
            Assert.IsTrue(crit.Type == Operator.EQ);
            Assert.AreEqual("text", crit.Modifier);
            Assert.IsTrue(crit.Operand is UntypedValue);            
        }

        [TestMethod]
        public void SerializeChain()
        {
            var crit = new Criterium
            {
                ParamName = "par1",
                Modifier = "type1",
                Type = Operator.CHAIN,
                Operand =
                    new Criterium
                    {
                        ParamName = "par2",
                        Type = Operator.CHAIN,
                        Operand =
                            new Criterium { ParamName = "par3", Modifier = "text", Type = Operator.EQ, Operand = new StringValue("hoi") }
                    }
            };

            Assert.AreEqual("par1:type1.par2.par3:text", crit.BuildKey());
            Assert.AreEqual("hoi", crit.BuildValue());
        }

        [TestMethod]
        public void SerializeCriterium()
        {
            var crit = new Criterium
            {  ParamName = "paramX", Modifier = "modif1", Operand = new NumberValue(18), Type = Operator.GTE };
            Assert.AreEqual("paramX:modif1", crit.BuildKey());
            Assert.AreEqual(">=18", crit.BuildValue());

            crit = new Criterium
            { ParamName = "paramX", Operand = new NumberValue(18) };
            Assert.AreEqual("paramX", crit.BuildKey());
            Assert.AreEqual("18", crit.BuildValue());

            crit = new Criterium
            { ParamName = "paramX",Type = Operator.ISNULL };
            Assert.AreEqual("paramX:missing", crit.BuildKey());
            Assert.AreEqual("true", crit.BuildValue());

            crit = new Criterium
            { ParamName = "paramX", Type = Operator.NOTNULL };
            Assert.AreEqual("paramX:missing", crit.BuildKey());
            Assert.AreEqual("false", crit.BuildValue());
        }


        [TestMethod]
        public void HandleNumberParam()
        {
            var p1 = new NumberValue(18);
            Assert.AreEqual("18", p1.ToString());

            var p2 = NumberValue.Parse("18");
            Assert.AreEqual(18M, p2.Value);

            var p3 = NumberValue.Parse("18.00");
            Assert.AreEqual(18.00M, p3.Value);

            var crit = Criterium.Parse("paramX", "18.34");
            var p4 = ((UntypedValue)crit.Operand).AsNumberValue();
            Assert.AreEqual(18.34M, p4.Value);
        }

        [TestMethod]
        public void HandleDateParam()
        {
            var p1 = new DateValue(new DateTimeOffset(1972, 11, 30, 15, 20, 49, TimeSpan.Zero));
            Assert.AreEqual("1972-11-30T15:20:49+00:00", p1.ToString());

            var p2 = DateValue.Parse("1972-11-30T18:45:36");
            Assert.AreEqual("1972-11-30T18:45:36", p2.ToString());

            var crit = Criterium.Parse("paramX", "1972-11-30");
            var p3 = ((UntypedValue)crit.Operand).AsDateValue();
            Assert.AreEqual("1972-11-30", p3.Value);
        }


        [TestMethod]
        public void HandleStringParam()
        {
            var p1 = new StringValue("Hello, world");
            Assert.AreEqual(@"Hello\, world", p1.ToString());

            var p2 = new StringValue("Pay $300|Pay $100|");
            Assert.AreEqual(@"Pay \$300\|Pay \$100\|", p2.ToString());

            var p3 = StringValue.Parse(@"Pay \$300\|Pay \$100\|");
            Assert.AreEqual("Pay $300|Pay $100|", p3.Value);

            var crit = Criterium.Parse("paramX", @"Hello\, world");
            var p4 = ((UntypedValue)crit.Operand).AsStringValue();
            Assert.AreEqual("Hello, world", p4.Value);
        }


        [TestMethod]
        public void HandleTokenParam()
        {
            var p1 = new TokenValue("NOK", "http://somewhere.nl/codes");
            Assert.AreEqual("http://somewhere.nl/codes|NOK", p1.ToString());

            var p2 = new TokenValue("y|n", "http://some|where.nl/codes");
            Assert.AreEqual(@"http://some\|where.nl/codes|y\|n", p2.ToString());

            var p3 = new TokenValue("NOK", matchAnyNamespace: true);
            Assert.AreEqual("NOK", p3.ToString());

            var p4 = new TokenValue("NOK", matchAnyNamespace: false);
            Assert.AreEqual("|NOK", p4.ToString());

            var p5 = TokenValue.Parse("http://somewhere.nl/codes|NOK");
            Assert.AreEqual("http://somewhere.nl/codes", p5.Namespace);
            Assert.AreEqual("NOK", p5.Value);
            Assert.IsFalse(p4.AnyNamespace);

            var p6 = TokenValue.Parse(@"http://some\|where.nl/codes|y\|n");
            Assert.AreEqual(@"http://some|where.nl/codes", p6.Namespace);
            Assert.AreEqual("y|n", p6.Value);
            Assert.IsFalse(p6.AnyNamespace);

            var p7 = TokenValue.Parse("|NOK");
            Assert.AreEqual(null, p7.Namespace);
            Assert.AreEqual("NOK", p7.Value);
            Assert.IsFalse(p7.AnyNamespace);

            var p8 = TokenValue.Parse("NOK");
            Assert.AreEqual(null, p8.Namespace);
            Assert.AreEqual("NOK", p8.Value);
            Assert.IsTrue(p8.AnyNamespace);

            var crit = Criterium.Parse("paramX", @"|NOK");
            var p9 = ((UntypedValue)crit.Operand).AsTokenValue();
            Assert.AreEqual("NOK", p9.Value);
            Assert.IsFalse(p9.AnyNamespace);
        }


        [TestMethod]
        public void HandleQuantityParam()
        {
            var p1 = new QuantityValue(3.141M, "http://unitsofmeasure.org", "mg");
            Assert.AreEqual("3.141|http://unitsofmeasure.org|mg", p1.ToString());

            var p2 = new QuantityValue(3.141M, "mg");
            Assert.AreEqual("3.141||mg", p2.ToString());

            var p3 = new QuantityValue(3.141M, "http://system.com/id$4", "$/d");
            Assert.AreEqual(@"3.141|http://system.com/id\$4|\$/d", p3.ToString());

            var p4 = QuantityValue.Parse("3.141|http://unitsofmeasure.org|mg");
            Assert.AreEqual(3.141M, p4.Number);
            Assert.AreEqual("http://unitsofmeasure.org", p4.Namespace);
            Assert.AreEqual("mg", p4.Unit);

            var p5 = QuantityValue.Parse("3.141||mg");
            Assert.AreEqual(3.141M, p5.Number);
            Assert.IsNull(p5.Namespace);
            Assert.AreEqual("mg", p5.Unit);

            var p6 = QuantityValue.Parse(@"3.141|http://system.com/id\$4|\$/d");
            Assert.AreEqual(3.141M, p6.Number);
            Assert.AreEqual("http://system.com/id$4",p6.Namespace);
            Assert.AreEqual("$/d", p6.Unit);

            var crit = Criterium.Parse("paramX", @"3.14||mg");
            var p7 = ((UntypedValue)crit.Operand).AsQuantityValue();
            Assert.AreEqual(3.14M, p7.Number);
            Assert.IsNull(p7.Namespace);
            Assert.AreEqual("mg", p7.Unit);
        }

        [TestMethod]
        public void HandleReferenceParam()
        {
            var p1 = new ReferenceValue("2");
            Assert.AreEqual("2", p1.Value);

            var p2 = new ReferenceValue("http://server.org/fhir/Patient/1");
            Assert.AreEqual("http://server.org/fhir/Patient/1", p2.Value);

            var crit = Criterium.Parse("paramX", @"http://server.org/\$4/fhir/Patient/1");
            var p3 = ((UntypedValue)crit.Operand).AsReferenceValue();
            Assert.AreEqual("http://server.org/$4/fhir/Patient/1", p3.Value);
        }

        [TestMethod]
        public void HandleMultiValueParam()
        {
            var p1 = new MultiValue(new ValueExpression[] { new StringValue("hello, world!"), new NumberValue(18.4M) });
            Assert.AreEqual(@"hello\, world!,18.4", p1.ToString());

            var p2 = MultiValue.Parse(@"hello\, world!,18.4");
            Assert.AreEqual(2, p2.Value.Length);
            Assert.AreEqual("hello, world!", ((UntypedValue)p2.Value[0]).AsStringValue().Value);
            Assert.AreEqual(18.4M, ((UntypedValue)p2.Value[1]).AsNumberValue().Value);
        }



        //[TestMethod]
        //public void HandleCombinedParam()
        //{
        //    var p1 = new CombinedParamValue(new TokenParamValue("NOK"), new IntegerParamValue(18));
        //    Assert.AreEqual("NOK$18", p1.QueryValue);

        //    var p2 = CombinedParamValue.FromQueryValue("!NOK$>=18");
        //    Assert.AreEqual(2, p2.Values.Count());
        //    Assert.AreEqual("NOK", ((UntypedParamValue)p2.Values.First()).AsTokenParam().Value);
        //    Assert.AreEqual(18, ((UntypedParamValue)p2.Values.Skip(1).First()).AsIntegerParam().Value);
        //}

        //[TestMethod]
        //public void ParseSearchParam()
        //{
        //    var p1 = new SearchParam("dummy", "exact", new IntegerParamValue(ComparisonOperator.LTE,18),
        //            new CombinedParamValue( new StringParamValue("ewout"), new ReferenceParamValue("patient","1")));
        //    Assert.AreEqual("dummy:exact=<=18,\"ewout\"$patient/1", p1.QueryPair);

        //    var p2 = new SearchParam("name", isMissing:true);
        //    Assert.AreEqual("name:missing=true", p2.QueryPair);

        //    var p3 = SearchParam.FromQueryKeyAndValue("dummy:exact", "<=18,\"ewout\"$patient/1");
        //    Assert.AreEqual("dummy", p3.Name);
        //    Assert.AreEqual("exact", p3.Modifier);
        //    Assert.AreEqual(2, p3.Values.Count());
        //    Assert.AreEqual(18, ((UntypedParamValue)p3.Values.First()).AsIntegerParam().Value);
        //    Assert.AreEqual("\"ewout\"$patient/1", ((UntypedParamValue)p3.Values.Skip(1).First()).AsCombinedParam().QueryValue);
        //}
    }
}