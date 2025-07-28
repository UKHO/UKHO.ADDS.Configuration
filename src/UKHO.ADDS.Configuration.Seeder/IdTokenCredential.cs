using Azure.Core;

namespace UKHO.ADDS.Configuration.Seeder
{
    internal class IdTokenCredential(string token) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new(token, DateTimeOffset.UtcNow.AddMinutes(5));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => new(new AccessToken(token, DateTimeOffset.UtcNow.AddMinutes(5)));
    }
}
