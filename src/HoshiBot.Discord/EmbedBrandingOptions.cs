namespace HoshiBot.Discord;

// The base URL where HoshiBot.Web (a separate process/deployment) serves static assets
// like the officer portrait — read from config in HoshiBot.Host's Program.cs and handed
// in here as a plain record, so HoshiBot.Discord doesn't need its own IConfiguration
// package reference just for this one value.
public record EmbedBrandingOptions(string PublicWebBaseUrl);
