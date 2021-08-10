using System;
using System.Security;
using CitizenFX.Core.Native;

namespace RedM.External
{
	public class StateBag
	{
		public string Name { get; }

		internal StateBag(string name)
		{
			this.Name = name;
		}

		public dynamic Get(string key)
		{
			return API.GetStateBagValue(this.Name, key);
		}

		public dynamic this[string key]
		{
			get => Get(key);
		}
	}
}
