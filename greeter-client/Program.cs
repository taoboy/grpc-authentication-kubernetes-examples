// Copyright 2015 gRPC authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using Grpc.Core;
using Helloworld;
using System.Threading;
using JWT;
using JWT.Builder;
using JWT.Algorithms;


namespace GreeterClient
{
    class Program
    {
        public static void Greet()
        {
            var channelTarget = Environment.GetEnvironmentVariable("GREETER_SERVICE_TARGET");
            Console.WriteLine("Creating channel with target " + channelTarget);

            ChannelCredentials channelCredentials = null;
            var securityOption = Environment.GetEnvironmentVariable("GREETER_CLIENT_SECURITY");
            if (securityOption == "insecure")
            {
                channelCredentials = ChannelCredentials.Insecure;
            }
            else if (securityOption == "tls")
            {
                channelCredentials = CreateCredentials(mutualTls: false, useJwt: false);
            }
            else if (securityOption == "jwt")
            {
                channelCredentials = CreateCredentials(mutualTls: false, useJwt: true);
            }
            else if (securityOption == "mtls")
            {
                channelCredentials = CreateCredentials(mutualTls: true, useJwt: false);
            }
            else 
            {
                throw new ArgumentException("Illegal security option.");
            }
            Console.WriteLine("Starting client with security: " + securityOption);

            Channel channel = new Channel(channelTarget, channelCredentials);

            var client = new Greeter.GreeterClient(channel);
            String user = "you";
            
            for (int i = 0; i < 10000; i++)
            {
                try {
                  var reply = client.SayHello(new HelloRequest { Name = user });
                  Console.WriteLine("Greeting: " + reply.Message);
                } 
                catch (RpcException e)
                {
                   Console.WriteLine("Error invoking greeting: " + e.Status);
                }
                
                Thread.Sleep(1000);
            }
            channel.ShutdownAsync().Wait();
            Console.WriteLine();
        }
        public static void Main(string[] args)
        {
            Greet();
        }

        private static string GenerateJwt()
        {
          //  String signedJwt = JWT.create()
        //.withKeyId(privateKeyId)
        //.withIssuer("123456-compute@developer.gserviceaccount.com")
        //.withSubject("123456-compute@developer.gserviceaccount.com")
        //.withAudience("https://firestore.googleapis.com/google.firestore.v1beta1.Firestore")
        //.withIssuedAt(new Date(now))
        //.withExpiresAt(new Date(now + 3600 * 1000L))
        //.sign(algorithm);

            //var timestamp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
            var token = new JwtBuilder()
                .WithAlgorithm(new HMACSHA256Algorithm())
                // TODO: add a real secret
                .WithSecret("dffaaf")
                // TODO: add keyId?
                // TODO: add iat
                .AddClaim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds())
                .AddClaim("iss", "demo-service-account@localhost")
                .AddClaim("sub", "demo-service-account@localhost")
                .AddClaim("aud", "demoservice1")
                .Build();

            Console.WriteLine(token);
            return token;
        }

        private static ChannelCredentials CreateCredentials(bool mutualTls, bool useJwt)
        {
            var certsPath = Environment.GetEnvironmentVariable("CERTS_PATH");

            var caRoots = File.ReadAllText(Path.Combine(certsPath, "ca.pem"));
            ChannelCredentials channelCredentials;
            if (!mutualTls)
            {
                channelCredentials = new SslCredentials(caRoots);
            }
            else
            {
                var keyCertPair = new KeyCertificatePair(
                File.ReadAllText(Path.Combine(certsPath, "client.pem")),
                File.ReadAllText(Path.Combine(certsPath, "client.key")));
                channelCredentials = new SslCredentials(caRoots, keyCertPair);
            }
    
            if (useJwt)
            {
                var authInterceptor = new AsyncAuthInterceptor(async (context, metadata) =>
                {
                    metadata.Add(new Metadata.Entry("authorization", "Bearer " + GenerateJwt()));
                });

                var metadataCredentials = CallCredentials.FromInterceptor(authInterceptor);
                channelCredentials = ChannelCredentials.Create(channelCredentials, metadataCredentials); 
            }
            return channelCredentials;
        }
    }
}
