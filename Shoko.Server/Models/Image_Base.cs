#nullable enable
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeMapping;
using Polly;
using Polly.Retry;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Extensions;
using Shoko.Server.Utilities;

namespace Shoko.Server.Models;

public class Image_Base : IImageMetadata
{
    #region Static Fields

    private static readonly object _lockObj = new();

    private static ILogger<Image_Base>? _logger;

    private static ILogger<Image_Base> Logger
    {
        get
        {
            if (_logger is not null)
                return _logger;

            lock (_lockObj)
            {
                _logger = Utils.ServiceContainer.GetService<ILogger<Image_Base>>()!;
                return _logger;
            }
        }
    }

    private static HttpClient? _httpClient;

    private static HttpClient Client
    {
        get
        {
            if (_httpClient is not null)
                return _httpClient;

            lock (_lockObj)
            {
                if (_httpClient is not null)
                    return _httpClient;
                _httpClient = new();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "JMM");
                _httpClient.Timeout = TimeSpan.FromMinutes(3);
                return _httpClient;
            }
        }
    }

    private static readonly TimeSpan[] _retryTimeSpans = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    private static readonly AsyncRetryPolicy _retryPolicy = Policy
        .Handle<HttpRequestException>()
        .Or<TaskCanceledException>()
        .WaitAndRetryAsync(_retryTimeSpans, (exception, timeSpan) =>
        {
            if (timeSpan == _retryTimeSpans[3] || (exception is HttpRequestException hre && hre.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden))
                throw exception;
        });

    #endregion

    private int InternalID { get; init; }

    /// <inheritdoc/>
    [NotMapped]
    public virtual int ID
    {
        get => InternalID;
        init => InternalID = value;
    }

    private string? _contentType;

    [NotMapped]
    public string ContentType
    {
        get
        {
            if (_contentType is not null)
                return _contentType;

            if (!string.IsNullOrEmpty(LocalPath))
                return _contentType = MimeUtility.GetMimeMapping(LocalPath);

            return MimeUtility.UnknownMimeType;
        }
    }

    /// <inheritdoc/>
    [NotMapped]
    public DataSourceEnum Source { get; init; }

    /// <inheritdoc/>
    [NotMapped]
    public ImageEntityType ImageType { get; init; }

    /// <inheritdoc/>
    [NotMapped]
    public bool IsPreferred { get; set; }

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <inheritdoc/>
    [NotMapped]
    public virtual bool IsLocked => true;

    [MemberNotNullWhen(true, nameof(LocalPath))]
    [NotMapped]
    public bool IsLocalAvailable
    {
        get => !string.IsNullOrEmpty(LocalPath) && File.Exists(LocalPath) && ImageExtensions.IsImageValid(LocalPath);
    }

    private bool? _urlExists;

    [MemberNotNullWhen(true, nameof(RemoteURL))]
    [NotMapped]
    public bool IsRemoteAvailable
    {
        get
        {
            if (_urlExists.HasValue)
                return _urlExists.Value;
            lock (this)
                return CheckIsRemoteAvailableAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }

    [MemberNotNullWhen(true, nameof(RemoteURL))]
    private async Task<bool> CheckIsRemoteAvailableAsync()
    {
        if (_urlExists.HasValue)
            return _urlExists.Value;

        if (string.IsNullOrEmpty(RemoteURL))
        {
            _urlExists = false;
            return false;
        }

        try
        {
            var stream = await _retryPolicy.ExecuteAsync(async () => await Client.GetStreamAsync(RemoteURL));
            var bytes = new byte[12];
            stream.Read(bytes, 0, 12);
            stream.Close();
            _urlExists = ImageExtensions.IsImageValid(bytes);
            return _urlExists.Value;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to retrieve resource at url: {RemoteURL}", RemoteURL);
            _urlExists = false;
            return false;
        }
    }

    /// <inheritdoc/>
    [NotMapped]
    public double AspectRatio
        => (double)Width / Height;

    internal int? _width;

    /// <inheritdoc/>
    public virtual int Width
    {
        get
        {
            if (_width.HasValue)
                return _width.Value;

            RefreshMetadata();

            return _width ?? 0;
        }
        set => _width = value; 
    }

    internal int? _height;

    /// <inheritdoc/>
    public virtual int Height
    {
        get
        {
            if (_height.HasValue)
                return _height.Value;

            RefreshMetadata();

            return _height ?? 0;
        }
        set => _height = value;
    }

    /// <inheritdoc/>
    [NotMapped]
    public string? LanguageCode
    {
        get => Language == TitleLanguage.Unknown ? null : Language.GetString();
        set => Language = value?.GetTitleLanguage() ?? TitleLanguage.Unknown;
    }

    /// <inheritdoc/>
    public TitleLanguage Language { get; set; }

    private string? _remoteURL;

    /// <inheritdoc/>
    [NotMapped]
    public virtual string? RemoteURL
    {
        get => _remoteURL;
        init
        {
            _contentType = null;
            _width = null;
            _height = null;
            _urlExists = null;
            _remoteURL = value;
        }
    }

    private string? _localPath;

    /// <inheritdoc/>
    [NotMapped]
    public virtual string? LocalPath
    {
        get => _localPath;
        set
        {
            _contentType = null;
            _width = null;
            _height = null;
            _localPath = value;
        }
    }

    private void RefreshMetadata()
    {
        try
        {
            var stream = GetStream();
            if (stream == null)
            {
                _width = 0;
                _height = 0;
                return;
            }

            var info = new MagickImageInfo(stream);
            _width = info.Width;
            _height = info.Height;
        }
        catch
        {
            _width = 0;
            _height = 0;
        }
    }

    public Stream? GetStream()
    {
        return IsLocalAvailable ? new FileStream(LocalPath, FileMode.Open, FileAccess.Read) : null;
    }

    public async Task<bool> DownloadImage(bool force = false)
    {
        if (string.IsNullOrEmpty(LocalPath) || string.IsNullOrEmpty(RemoteURL))
            return false;

        if (!force && IsLocalAvailable)
            return true;

        var binary = await _retryPolicy.ExecuteAsync(async () => await Client.GetByteArrayAsync(RemoteURL));
        if (!ImageExtensions.IsImageValid(binary))
            throw new HttpRequestException($"Invalid image data format at remote resource: {RemoteURL}", null, HttpStatusCode.ExpectationFailed);

        // Ensure directory structure exists.
        var dirPath = Path.GetDirectoryName(LocalPath);
        if (!string.IsNullOrEmpty(dirPath))
            Directory.CreateDirectory(dirPath);

        // Delete existing file if re-downloading.
        if (File.Exists(LocalPath))
            File.Delete(LocalPath);

        // Write the memory-cached image onto the disk.
        await File.WriteAllBytesAsync(LocalPath, binary);

        // "Flush" the cached image.
        _urlExists = null;

        return true;
    }

    public override int GetHashCode()
        => HashCode.Combine(ID, Source, ImageType);

    public override bool Equals(object? other)
    {
        if (other is null || other is not IImageMetadata imageMetadata)
            return false;
        return Equals(imageMetadata);
    }

    public bool Equals(IImageMetadata? other)
    {
        if (other is null)
            return false;
        return other.Source == Source &&
            other.ImageType == ImageType &&
            other.ID == ID;
    }
}
