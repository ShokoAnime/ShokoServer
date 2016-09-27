using Nancy;
using Nancy.Responses;
using Nancy.Responses.Negotiation;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JMMServer.API
{
	public class BinaryProcessor : IResponseProcessor
	{
		public static IList<Tuple<string, MediaRange>> Mappings { get; set; }

	    private static Logger logger = LogManager.GetCurrentClassLogger();
	    static BinaryProcessor()
		{
			Mappings = new List<Tuple<string, MediaRange>>();
		}

		public IEnumerable<Tuple<string, MediaRange>> ExtensionMappings
		{
			get { return Mappings.ToArray(); }
		}

		public ProcessorMatch CanProcess(MediaRange requestedMediaRange, dynamic model, NancyContext context)
		{
			var acceptableType = (model != null && (model.GetType() == typeof(byte[]) || model is Stream));
			var modelResult = acceptableType ? MatchResult.ExactMatch : MatchResult.NoMatch;
			var contentTypeResult = Mappings.Any(map => map.Item2.Matches(requestedMediaRange)) ? MatchResult.ExactMatch : MatchResult.NoMatch;

			return new ProcessorMatch { ModelResult = modelResult, RequestedContentTypeResult = contentTypeResult };
		}

		public Response Process(MediaRange requestedMediaRange, dynamic model, NancyContext context)
		{
			if (model is Stream)
			{
				return new StreamResponse(() => model, requestedMediaRange);
			}

			return new ByteArrayResponse((byte[])model, requestedMediaRange);
		}
	}
}
