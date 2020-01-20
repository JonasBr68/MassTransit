namespace MassTransit.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Mime;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text;
    using GreenPipes;
    using GreenPipes.Internals.Extensions;
    using GreenPipes.Internals.Reflection;
    using MassTransit.Metadata;
    using Newtonsoft.Json;


    public interface IMessageTypeResolver
    {
        Type GetMostDerivedMessageType(IEnumerable<Type> types);
    }
    public class MessageTypeResolver : IMessageTypeResolver
    {
        public Type GetMostDerivedMessageType(IEnumerable<Type> types)
        {
            Type bestCandidate = null;

            //Gets the most concrete implementation
            var candidates = types.Where(t => t != null && !t.IsInterface && !t.IsAbstract).OrderByDescending(t => t.GetTypeInfo().ImplementedInterfaces.Count());

            bestCandidate = candidates.FirstOrDefault();


            if (bestCandidate == null)
            {
                candidates = types.Where(t => t != null && t.IsInterface).OrderByDescending(t => t.GetTypeInfo().ImplementedInterfaces.Count());
                //Does the best candicate interface actually inherit from all the others?
                bestCandidate = candidates.FirstOrDefault();
            }

            if (bestCandidate != null)
            {
                var interfaceTypes = types.Where(t => t != null && t.IsInterface);
                var allInterfaces = interfaceTypes.Union(interfaceTypes.SelectMany(t => t.GetTypeInfo().ImplementedInterfaces)).Distinct();

                var candidatesInterface = bestCandidate.IsInterface ?
                                            bestCandidate.GetTypeInfo().ImplementedInterfaces.Union(new Type[] { bestCandidate }) :
                                            bestCandidate.GetTypeInfo().ImplementedInterfaces;

                var missingInterfaces = allInterfaces.Except(candidatesInterface);
                if (missingInterfaces.Any())
                {
                    Trace.WriteLine($"{bestCandidate.Name} does not inherit from {string.Join(", ", missingInterfaces)}");
                    bestCandidate = null;
                }
            }
            return bestCandidate;
        }
    }

    public class JsonStronglyTypedMessageDeserializer :
        IMessageDeserializer
    {
        readonly JsonSerializer _deserializer;
        readonly static IImplementationBuilderExt _implementationBuilderExt = new ExtendedDynamicImplementationBuilder();
    
        public JsonStronglyTypedMessageDeserializer(JsonSerializer deserializer)
        {
            _deserializer = deserializer;
        }

        void IProbeSite.Probe(ProbeContext context)
        {
            var scope = context.CreateScope("json");
            scope.Add("contentType", JsonMessageSerializer.JsonContentType.MediaType);
        }

        ContentType IMessageDeserializer.ContentType => JsonMessageSerializer.JsonContentType;

        ConsumeContext IMessageDeserializer.Deserialize(ReceiveContext receiveContext)
        {
            try
            {
                var messageEncoding = GetMessageEncoding(receiveContext);

                List<Type> messageTypes = null;
                using (var body = receiveContext.GetBodyStream())
                using (var streamReader = new StreamReader(body, messageEncoding, false, 1024, true))
                {
                    string[] messageTypeStrings = null;
                    using (var jsonReader = new JsonTextReader(streamReader))
                    {
                        while (jsonReader.Read())
                        {
                            if (jsonReader.TokenType == JsonToken.PropertyName &&
                                (string)jsonReader.Value == "messageType" &&
                                jsonReader.Read())
                            {
                                messageTypeStrings = _deserializer.Deserialize<string[]>(jsonReader);
                                break;
                            }
                            if (jsonReader.LineNumber > 100 || jsonReader.LinePosition > 1024 * 10)
                                throw new NotSupportedException("Unknown Masstransit message format");
                        }
                    }
                    streamReader.BaseStream.Seek(0, SeekOrigin.Begin);
                    streamReader.BaseStream.Position = 0;
                    messageTypes = messageTypeStrings.Select(s => MessageUrn.ForMessageUrnString(s)).ToList();

                }

                IMessageTypeResolver typeResolver = new MessageTypeResolver();

                var deserializeType = typeResolver.GetMostDerivedMessageType(messageTypes);

                if (deserializeType != null)
                {
                    Type openMetaDataCache = typeof(TypeMetadataCache<>);
                    Type genericMetaDataCache = openMetaDataCache.MakeGenericType(deserializeType);

                    var prop = genericMetaDataCache.GetProperty("IsValidMessageType");

                    bool isValidMessageType = (bool)prop.GetValue(null);

                    if (deserializeType.GetTypeInfo().IsInterface && isValidMessageType)
                        deserializeType = TypeCache.GetImplementationType(deserializeType);
                }
                else
                {
                    deserializeType = _implementationBuilderExt.GetImplementationType(messageTypes.Where(t => t.IsInterface).ToList());
                }

                Type openEvenlopeType = typeof(GenericMessageEnvelope<>);
                Type genericEvenlopeType = openEvenlopeType.MakeGenericType(deserializeType);


                MessageEnvelope envelope;
                using (var body = receiveContext.GetBodyStream())
                using (var reader = new StreamReader(body, messageEncoding, false, 1024, true))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    //envelope = _deserializer.Deserialize<MessageEnvelope>(jsonReader);
                    envelope = (MessageEnvelope)_deserializer.Deserialize(jsonReader, genericEvenlopeType);
                }

                return new JsonConsumeContext(_deserializer, receiveContext, envelope);
            }
            catch (JsonSerializationException ex)
            {
                throw new SerializationException("A JSON serialization exception occurred while deserializing the message envelope", ex);
            }
            catch (SerializationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SerializationException("An exception occurred while deserializing the message envelope", ex);
            }
        }

        static Encoding GetMessageEncoding(ReceiveContext receiveContext)
        {
            var contentEncoding = receiveContext.TransportHeaders.Get("Content-Encoding", default(string));

            return string.IsNullOrWhiteSpace(contentEncoding) ? Encoding.UTF8 : Encoding.GetEncoding(contentEncoding);
        }
    }
}
