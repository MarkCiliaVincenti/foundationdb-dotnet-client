﻿#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Diagnostics.Contracts
{
	using System;
	using System.Runtime.Serialization;
	using System.Security;
	using SDC = System.Diagnostics.Contracts;

	[Serializable]
	public sealed class ContractException : Exception
	{
		// copie de l'implémentation "internal" de System.Data.Contracts.ContractException

		#region Constructors...

		private ContractException()
		{
			base.HResult = -2146233022;
		}

		public ContractException(SDC.ContractFailureKind kind, string? failure, string? userMessage, string? condition, Exception? innerException)
			: base(failure, innerException)
		{
			base.HResult = -2146233022;
			this.Kind = kind;
			this.UserMessage = userMessage;
			this.Condition = condition;
		}

		private ContractException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.Kind = (SDC.ContractFailureKind)info.GetInt32("Kind");
			this.UserMessage = info.GetString("UserMessage");
			this.Condition = info.GetString("Condition");
		}

		#endregion

		#region Public Properties...

		public string? Condition { get; }

		public SDC.ContractFailureKind Kind { get; }

		public string? UserMessage { get; }

		public string? Failure => this.Message;

		#endregion

		[SecurityCritical]
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("Kind", (int) this.Kind);
			info.AddValue("UserMessage", this.UserMessage);
			info.AddValue("Condition", this.Condition);
		}

	}

}
