using System;

namespace xc17_prom_prog
{
	[Serializable]
	public class InvalidResponseException : Exception
	{
		public InvalidResponseException() : base("Invalid target response.")
		{
		}

		public InvalidResponseException(string message) : base(message)
		{
		}

		public InvalidResponseException(string message, Exception innerException) : base(message, innerException)
		{
		}

		protected InvalidResponseException(System.Runtime.Serialization.SerializationInfo info,
			System.Runtime.Serialization.StreamingContext context) : base(info, context)
		{
		}
	}
}
