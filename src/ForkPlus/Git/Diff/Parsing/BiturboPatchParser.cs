using ForkPlus.Biturbo;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff.Parsing.Tokens;

namespace ForkPlus.Git.Diff.Parsing
{
    /// <summary>
    /// WPF-side patch parser that defers tokenization to the Biturbo FFI.
    /// Lives in the WPF project because Biturbo is the platform-specific
    /// backend; the platform-neutral parsing logic lives in
    /// <see cref="PatchParser"/> in ForkPlus.Core.
    /// </summary>
    public class BiturboPatchParser : PatchParser
    {
        protected override GitCommandResult<Token[]> ReadTokens(byte[] diffUtf8, byte[] srcPrefixUtf8, byte[] dstPrefixUtf8)
        {
            return BtRequest.Run(() => default(BtParsePatchResult), delegate(ref BtParsePatchResult x)
            {
                return Bt.bt_parse_patch(diffUtf8, (ulong)diffUtf8.Length, srcPrefixUtf8, (ulong)srcPrefixUtf8.Length, dstPrefixUtf8, (ulong)dstPrefixUtf8.Length, ref x);
            }, delegate(ref BtParsePatchResult x)
            {
                return Into(ref x);
            }, delegate(ref BtParsePatchResult x)
            {
                Bt.bt_release_parse_patch(ref x);
            });
        }

        private static GitCommandResult<Token[]> Into(ref BtParsePatchResult btParsePatchResult)
        {
            return GitCommandResult<Token[]>.Success(btParsePatchResult.tokens.GetStructArray(btParsePatchResult.tokens_len, delegate(BtPatchToken btPatchToken)
            {
                byte kind = btPatchToken.kind;
                Range range = new Range((int)btPatchToken.start, (int)btPatchToken.end);
                return new Token((TokenType)kind, range);
            }));
        }
    }
}
