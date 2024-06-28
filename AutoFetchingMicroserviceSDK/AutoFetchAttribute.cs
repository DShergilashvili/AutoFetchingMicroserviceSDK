namespace AutoFetchingMicroserviceSDK
{
    [AttributeUsage(AttributeTargets.Property)]
    public class AutoFetchAttribute : Attribute
    {
        public string MicroserviceName { get; }
        public string ForeignKeyPropertyName { get; }

        public AutoFetchAttribute(string microserviceName, string foreignKeyPropertyName)
        {
            MicroserviceName = microserviceName;
            ForeignKeyPropertyName = foreignKeyPropertyName;
        }
    }
}
