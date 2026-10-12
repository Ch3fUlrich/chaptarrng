using System;
using System.Collections.Generic;
using FluentValidation.Results;
using MQTTnet;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Notifications.MqttGateway
{
    public class MqttGateway : NotificationBase<MqttGatewaySettings>, IResolveProviderPendingSecrets
    {
        private readonly IPendingProviderSecretService _pendingProviderSecretService;

        public MqttGateway(IPendingProviderSecretService pendingProviderSecretService)
        {
            _pendingProviderSecretService = pendingProviderSecretService;
        }

        public override string Name => "MQTT Gateway";
        public override string Link => "https://mqtt.org/";

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();
            IMqttClient client = null;

            try
            {
                var rootTopic = MqttGatewayMqtt.GetRootTopic(Settings);
                var availabilityTopic = $"{rootTopic}/status";
                var factory = new MqttClientFactory();
                client = factory.CreateMqttClient();
                var connectResult = client.ConnectAsync(MqttGatewayMqtt.BuildClientOptions(Settings, availabilityTopic)).GetAwaiter().GetResult();

                if (!client.IsConnected)
                {
                    failures.Add(new ValidationFailure("Host", $"The MQTT broker rejected the connection ({connectResult.ResultCode})."));
                    return new ValidationResult(failures);
                }

                var testEvent = "{\"schemaVersion\":1,\"event\":\"test\",\"occurredAt\":\"" + DateTime.UtcNow.ToString("O") + "\"}";
                var message = MqttGatewayMqtt.BuildMessage($"{rootTopic}/events/test", testEvent, Settings.EventQos);
                client.PublishAsync(message).GetAwaiter().GetResult();
                client.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().Build()).GetAwaiter().GetResult();
            }
            catch
            {
                failures.Add(new ValidationFailure("Host", "Unable to connect to the MQTT broker. Check the host, port, TLS configuration, and credentials."));
            }
            finally
            {
                client?.Dispose();
            }

            return new ValidationResult(failures);
        }

        public void ResolveProviderPendingSecrets(bool consume)
        {
            Settings.Password = _pendingProviderSecretService.Resolve(Settings.Password, consume);
        }
    }
}
