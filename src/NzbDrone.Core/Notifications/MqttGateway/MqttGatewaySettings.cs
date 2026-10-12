using System;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.MqttGateway
{
    public enum MqttGatewayQos
    {
        AtMostOnce = 0,
        AtLeastOnce = 1,
        ExactlyOnce = 2
    }

    public class MqttGatewaySettingsValidator : AbstractValidator<MqttGatewaySettings>
    {
        public MqttGatewaySettingsValidator()
        {
            RuleFor(c => c.Host).NotEmpty().ValidHost();
            RuleFor(c => c.Port).ValidPort();
            RuleFor(c => c.InstanceId).NotEmpty().MaximumLength(64).Matches("^[A-Za-z0-9_-]+$");
            RuleFor(c => c.TopicPrefix).NotEmpty().MaximumLength(128).Must(prefix => !string.IsNullOrWhiteSpace(prefix) && !prefix.Contains("//") && !prefix.StartsWith("/") && !prefix.EndsWith("/"));
            RuleFor(c => c.ClientId).MaximumLength(128);
            RuleFor(c => c.DiscoveryPrefix).NotEmpty().When(c => c.PublishHomeAssistantDiscovery);
            RuleFor(c => c.SensorRefreshIntervalMinutes).InclusiveBetween(1, 1440);
            RuleFor(c => c.UpcomingReleaseWindowDays).InclusiveBetween(1, 365);
        }
    }

    public class MqttGatewaySettings : IProviderConfig
    {
        private static readonly MqttGatewaySettingsValidator Validator = new();

        public MqttGatewaySettings()
        {
            InstanceId = Guid.NewGuid().ToString("N");
        }

        [FieldDefinition(0, Label = "Broker Host", Type = FieldType.Textbox, HelpText = "Hostname or IP address of the MQTT broker")]
        public string Host { get; set; }

        [FieldDefinition(1, Label = "Broker Port", Type = FieldType.Number, HelpText = "1883 without TLS, 8883 with TLS")]
        public int Port { get; set; } = 1883;

        [FieldDefinition(2, Label = "Username", Privacy = PrivacyLevel.UserName)]
        public string Username { get; set; }

        [FieldDefinition(3, Label = "Password", Type = FieldType.Password, Privacy = PrivacyLevel.Password)]
        public string Password { get; set; }

        [FieldDefinition(4, Label = "Use TLS", Type = FieldType.Checkbox, HelpText = "Connect using TLS. Server certificates are validated by default.")]
        public bool UseTls { get; set; }

        [FieldDefinition(5, Label = "Allow Invalid TLS Certificates", Type = FieldType.Checkbox, Advanced = true, HelpTextWarning = "Disables broker certificate validation. Use only with a trusted private broker.")]
        public bool AllowInvalidCertificates { get; set; }

        [FieldDefinition(6, Label = "Instance ID", Type = FieldType.Textbox, Advanced = true, HelpText = "Stable identifier used to keep topics and Home Assistant entities unique across Chaptarr instances")]
        public string InstanceId { get; set; }

        [FieldDefinition(7, Label = "Client ID", Type = FieldType.Textbox, HelpText = "Leave blank to generate a stable client ID from the instance ID")]
        public string ClientId { get; set; }

        [FieldDefinition(8, Label = "Topic Prefix", Type = FieldType.Textbox, HelpText = "Default: chaptarr")]
        public string TopicPrefix { get; set; } = "chaptarr";

        [FieldDefinition(9, Label = "Event QoS", Type = FieldType.Select, SelectOptions = typeof(MqttGatewayQos))]
        public MqttGatewayQos EventQos { get; set; } = MqttGatewayQos.AtLeastOnce;

        [FieldDefinition(10, Label = "Publish Sensors", Type = FieldType.Checkbox)]
        public bool PublishSensors { get; set; } = true;

        [FieldDefinition(11, Label = "Publish Events", Type = FieldType.Checkbox)]
        public bool PublishEvents { get; set; } = true;

        [FieldDefinition(12, Label = "Home Assistant Discovery", Type = FieldType.Checkbox)]
        public bool PublishHomeAssistantDiscovery { get; set; } = true;

        [FieldDefinition(13, Label = "Home Assistant Discovery Prefix", Type = FieldType.Textbox, HelpText = "Default: homeassistant")]
        public string DiscoveryPrefix { get; set; } = "homeassistant";

        [FieldDefinition(14, Label = "Sensor Refresh Interval", Type = FieldType.Number, Unit = "minutes")]
        public int SensorRefreshIntervalMinutes { get; set; } = 5;

        [FieldDefinition(15, Label = "Upcoming Release Window", Type = FieldType.Number, Unit = "days")]
        public int UpcomingReleaseWindowDays { get; set; } = 30;

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
