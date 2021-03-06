using System;
using System.Collections.Generic;
using System.Data;
using System.Xml;
using NS.Base;
using NS.Models.Base;

namespace NS.Models
{
	public partial class NullableTable : BaseModel
	{
		public override string EntityName => "NullableTable";
		private Int32 _id;
		private Int32? _age;
		private DateTime? _dob;
		private Guid? _lolval;

		public virtual Int32 Id
		{
			get => _id;
			set => SetValue(ref _id, value);
		}
		public virtual Int32? Age
		{
			get => _age;
			set => SetValue(ref _age, value);
		}
		public virtual DateTime? DoB
		{
			get => _dob;
			set => SetValue(ref _dob, value);
		}
		public virtual Guid? lolVal
		{
			get => _lolval;
			set => SetValue(ref _lolval, value);
		}
		public override IBaseModel SetValues(DataRow row, string propertyPrefix)
		{
			_id = row.GetValue<Int32>($"{propertyPrefix}Id") ?? default(Int32); 
			_age = row.GetValue<Int32>($"{propertyPrefix}Age"); 
			_dob = row.GetValue<DateTime>($"{propertyPrefix}DoB"); 
			_lolval = row.GetValue<Guid>($"{propertyPrefix}lolVal"); 
			return this;
		}
		public override List<ValidationError> Validate()
		{
			var validationErrors = new List<ValidationError>();

			if (DoB == DateTime.MinValue)
				validationErrors.Add(new ValidationError(nameof(DoB), "Value cannot be default."));
			if (lolVal == Guid.Empty)
				validationErrors.Add(new ValidationError(nameof(lolVal), "Value cannot be default."));

			return validationErrors;
		}
		public static List<ColumnDefinition> Columns => new List<ColumnDefinition>
		{
			new ColumnDefinition("Id", typeof(System.Int32), "[INT]", SqlDbType.Int, false, true, true),
			new ColumnDefinition("Age", typeof(System.Int32), "[INT]", SqlDbType.Int, true, false, false),
			new ColumnDefinition("DoB", typeof(System.DateTime), "[DATETIME]", SqlDbType.DateTime, true, false, false),
			new ColumnDefinition("lolVal", typeof(System.Guid), "[UNIQUEIDENTIFIER]", SqlDbType.UniqueIdentifier, true, false, false),
		};
	}
}

