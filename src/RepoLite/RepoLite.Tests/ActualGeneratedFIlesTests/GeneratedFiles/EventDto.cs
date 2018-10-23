using System;
using System.Collections.Generic;
using System.Xml;
using NS.Models.Base;

namespace NS.Models
{
	public partial class Event : BaseModel
	{
		public override string EntityName => "Event";
		private String _eventid;
		private String _eventname;

		public virtual String EventId
		{
			get => _eventid;
			set => SetValue(ref _eventid, value);
		}
		public virtual String EventName
		{
			get => _eventname;
			set => SetValue(ref _eventname, value);
		}
		public override List<ValidationError> Validate()
		{
			var validationErrors = new List<ValidationError>();

			if (string.IsNullOrEmpty(EventId))
				validationErrors.Add(new ValidationError(nameof(EventId), "Value cannot be null"));
			if (!string.IsNullOrEmpty(EventId) && EventId.Length > 20)
				validationErrors.Add(new ValidationError(nameof(EventId), "Max length is 20"));
			if (string.IsNullOrEmpty(EventName))
				validationErrors.Add(new ValidationError(nameof(EventName), "Value cannot be null"));
			if (!string.IsNullOrEmpty(EventName) && EventName.Length > 100)
				validationErrors.Add(new ValidationError(nameof(EventName), "Max length is 100"));

			return validationErrors;
		}
	}
}

