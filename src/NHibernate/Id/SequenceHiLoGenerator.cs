using System;
using System.Data;
using System.Collections;
using System.Runtime.CompilerServices;

using NHibernate.Engine;
using NHibernate.Type;
using NHibernate.Util;
using NHibernate.Dialect;


namespace NHibernate.Id 
{
	/// <summary>
	/// An <see cref="IIdentifierGenerator" /> that combines a hi/lo algorithm with an underlying
	/// oracle-style sequence that generates hi values.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The user may specify a maximum lo value to determine how often new hi values are
	/// fetched. If sequences are not avaliable, <c>TableHiLoGenerator</c> might be an
	/// alternative.
	/// </para>
	/// <para>
	/// The mapping parameters supported are: <c>sequence</c>, <c>max_lo</c>
	/// </para>
	/// </remarks>
	public class SequenceHiLoGenerator : SequenceGenerator 
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(SequenceHiLoGenerator));

		public const string MaxLo = "max_lo";

		private int maxLo;
		private int lo;
		private long hi;
		private System.Type returnClass;

		public override void Configure(IType type, IDictionary parms, Dialect.Dialect d) 
		{
			base.Configure(type, parms, d);
			maxLo = PropertiesHelper.GetInt(MaxLo, parms, 9);
			lo = maxLo + 1; // so we "clock over" on the first invocation
			returnClass = type.ReturnedClass;
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public override object Generate(ISessionImplementor session, object obj) 
		{
			if ( lo > maxLo ) 
			{
				long hival = ( (long) base.Generate(session, obj) );
				lo = 1;
				hi = hival * ( maxLo+1 );
				log.Debug("new hi value: " + hival);
			}
			return IdentifierGeneratorFactory.CreateNumber( hi + lo++, returnClass );
		}
	}
}
