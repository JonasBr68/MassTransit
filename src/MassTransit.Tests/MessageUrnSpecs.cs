namespace MassTransit.Tests
{
    using System;
    using System.Diagnostics;
    using NUnit.Framework;
    using TestFramework.Messages;


    [TestFixture]
    public class MessageUrnSpecs
    {
        [Test]
        public void ComplexMessageUrns()
        {
            var urn1 = MessageUrn.ForType(typeof(PingMessage));
            Console.WriteLine($"PingMessage:\r\n\t{urn1.ToString()}\r\n");

            var urn2 = MessageUrn.ForType(typeof(G<X>));
            Console.WriteLine($"G<X>:\r\n\t{urn2.ToString()}\r\n");

            var urn3 = MessageUrn.ForType(typeof(GX<G<MassTransit.Tests.MessageUrnSpecs.X>>));
            Console.WriteLine($"GX<G<MassTransit.Tests.MessageUrnSpecs.X>>:\r\n\t{urn3.ToString()}\r\n");

            var urn4 = MessageUrn.ForType(typeof(GM<X,X>));
            Console.WriteLine($"GM<X,X>:\r\n\t{urn4.ToString()}\r\n");

        }

        //PingMessage:
        //	urn:message:MassTransit.TestFramework.Messages:PingMessage

        //G<X>:
        //	urn:message:MassTransit.Tests:G[[MassTransit.Tests:MessageUrnSpecs+X]]

        //GX<G<MassTransit.Tests.MessageUrnSpecs.X>>:
        //	urn:message:MassTransit.Tests:MessageUrnSpecs+GX[[MassTransit.Tests:G[[MassTransit.Tests:MessageUrnSpecs+X]]]]

        //GM<X, X>:
        //	urn:message:MassTransit.Tests:GM[[MassTransit.Tests:MessageUrnSpecs+X],[MassTransit.Tests:MessageUrnSpecs+X]]

        [Test]
        public void GetTypeFromSimpleMessageUrn()
        {
            var type1 = MessageUrn.ForMessageUrnString("urn:message:MassTransit.TestFramework.Messages:PingMessage");
            Assert.AreEqual(typeof(PingMessage), type1);


        }
        [Test]
        public void GetTypeFromGenericWithNestedTypeMessageUrn()
        {
            var type2 = MessageUrn.ForMessageUrnString("urn:message:MassTransit.Tests:G[[MassTransit.Tests:MessageUrnSpecs+X]]");
            Assert.AreEqual(typeof(G<X>), type2);

        }

        [Test]
        public void GetTypeFromGenericWithMultipleNestedTypesMessageUrn()
        {
            var type2 = MessageUrn.ForMessageUrnString("urn:message:MassTransit.Tests:GM[[MassTransit.Tests:MessageUrnSpecs+X],[MassTransit.Tests:MessageUrnSpecs+X]]");
            Assert.AreEqual(typeof(GM<X,X>), type2);

        }

        [Test]
        public void SimpleMessage()
        {

            var urn = MessageUrn.ForType(typeof (PingMessage));
            Assert.AreEqual(urn.AbsolutePath, "message:MassTransit.TestFramework.Messages:PingMessage");

        }

        [Test]
        public void NestedMessage()
        {
            var urn = MessageUrn.ForType(typeof (X));
            Assert.AreEqual(urn.AbsolutePath, "message:MassTransit.Tests:MessageUrnSpecs+X");
        }

        [Test]
        public void OpenGenericMessage()
        {
            Assert.That(() => MessageUrn.ForType(typeof(G<>)), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void ClosedGenericMessage()
        {
            var urn = MessageUrn.ForType(typeof(G<PingMessage>));
            var expected = new Uri("urn:message:MassTransit.Tests:G[[MassTransit.TestFramework.Messages:PingMessage]]");
            Assert.AreEqual(expected.AbsolutePath,urn.AbsolutePath) ;
        }

        class X{}

        class GX<T> { }
    }
    public class G<T>{}
    public class GM<T1,T2> { }
}
