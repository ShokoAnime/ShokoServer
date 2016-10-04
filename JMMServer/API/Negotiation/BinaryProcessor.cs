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
			Mappings = new List<Tuple<string, MediaRange>>()
			{
				new Tuple<string, MediaRange>("jpg", "image/jpeg"),
				new Tuple<string, MediaRange>("jpeg", "image/jpeg"),
				new Tuple<string, MediaRange>("png", "image/png")
			};

		}

		public IEnumerable<Tuple<string, MediaRange>> ExtensionMappings
		{
			get { return Mappings; }
		}

		public ProcessorMatch CanProcess(MediaRange requestedMediaRange, dynamic model, NancyContext context)
		{
			if (!context.Request.Url.Path.Contains("jmmserverimage", StringComparison.OrdinalIgnoreCase))
				return new ProcessorMatch {ModelResult = MatchResult.NoMatch, RequestedContentTypeResult = MatchResult.NoMatch};

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
