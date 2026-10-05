// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Security/SilentCatchMemberResolutionTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Guard rails — the silent-catch auditor's member attribution
//
// WHY THIS EXISTS
// ───────────────
// SilentCatchAuditTests allows an empty `catch { }` by (file, member) pair.
// The member name comes from FindContainingMethod, and until 2026-10-05 that
// method walked root.DescendantNodes() in document order and returned the
// FIRST method-like node before the catch — not the enclosing one. It also had
// no case for property accessors.
//
// Three consequences, all real, all measured on this tree:
//
//   • The catch inside RelayCommand.Execute resolved to the RelayCommand
//     ctor, because the ctor is declared above Execute in the same file.
//   • The catches in MainViewModel's property setters resolved to
//     "<unknown>" or to TogglePopup, whichever method came first.
//   • A catch in a local function resolved to the method declaring it.
//
// Because the allow list matches on that name, six findings could not be
// allowed by name at all and had to sit behind file-wide "*" entries — which
// silence every future empty catch in those files, including the ones nobody
// has reviewed. Fixing the resolver is what makes member-specific entries
// possible again, and this file is the net that keeps it fixed.
//
// WHAT IS ASSERTED
// ────────────────
// The resolved name for a catch in each of the four shapes the old walk got
// wrong: a method that is not the first member, a property setter, a local
// function, and a lambda. The fixture is deliberately ordered so that the
// first member in the file (a constructor) is NOT the one containing the
// first catch — that ordering is the whole bug.
// ═══════════════════════════════════════════════════════════════════════

using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace ProGlassAutomation.Tests.Security
{
    public class SilentCatchMemberResolutionTests
    {
        // Document order of the catches below: Run, IsBusy's setter,
        // LocalHelper, WithLambda. The constructor comes first in the file and
        // contains none of them — under the old document-order walk every one
        // of these resolved to "Sample".
        private const string Fixture = @"
public class Sample
{
    private readonly System.Action _work;

    public Sample(System.Action work)
    {
        _work = work;
    }

    public void Run()
    {
        try { _work(); }
        catch { }
    }

    public bool IsBusy
    {
        set
        {
            try { Raise(value); }
            catch { }
        }
    }

    public void Outer()
    {
        void LocalHelper()
        {
            try { DoStep(); }
            catch { }
        }
        LocalHelper();
    }

    public void WithLambda()
    {
        System.Threading.Tasks.Task.Run(() =>
        {
            try { DoStep(); }
            catch { }
        });
    }

    private static void Raise(bool v) { }
    private static void DoStep() { }
}
";

        [Fact]
        public void A_catch_in_a_method_that_is_not_the_first_member_resolves_to_that_method()
        {
            // The regression that started this: RelayCommand.cs:126 is inside
            // Execute but resolved to the ctor declared above it.
            Assert.Equal("Run", ResolveAll(Fixture)[0]);
        }

        [Fact]
        public void A_catch_in_a_property_setter_resolves_to_the_setter()
        {
            // There was no AccessorDeclarationSyntax case at all, so a setter
            // fell through to "<unknown>" — a name no allow-list entry can
            // usefully match, because "<unknown>" would also match a catch
            // that genuinely sits in no member.
            Assert.Equal("set_IsBusy", ResolveAll(Fixture)[1]);
        }

        [Fact]
        public void A_catch_in_a_local_function_resolves_to_the_local_function()
        {
            // The innermost member, not the method that declares it. A
            // best-effort cleanup helper is a local function more often than
            // not, and that is exactly where an empty catch is acceptable.
            Assert.Equal("LocalHelper", ResolveAll(Fixture)[2]);
        }

        [Fact]
        public void A_catch_in_a_lambda_resolves_to_the_member_containing_the_lambda()
        {
            // A lambda has no name of its own. The walk continues past it so
            // the catch is attributed to something an allow list can name,
            // rather than to "<unknown>".
            Assert.Equal("WithLambda", ResolveAll(Fixture)[3]);
        }

        [Fact]
        public void Every_shape_resolves_in_document_order()
        {
            // Guards the set as well as the order: adding a fifth shape to the
            // fixture without deciding what it should resolve to fails here.
            Assert.Equal(
                new[] { "Run", "set_IsBusy", "LocalHelper", "WithLambda" },
                ResolveAll(Fixture));
        }

        [Fact]
        public void An_indexer_and_an_event_accessor_are_named_after_their_owner()
        {
            const string owners = @"
public class Indexers
{
    public int this[int i]
    {
        set
        {
            try { Store(i, value); }
            catch { }
        }
    }

    public event System.EventHandler Changed
    {
        add
        {
            try { Hook(value); }
            catch { }
        }
        remove { }
    }

    private static void Store(int i, int v) { }
    private static void Hook(System.EventHandler h) { }
}
";
            // The keyword alone is useless in an allow list — a view model has
            // twenty setters — so the owner is part of the name.
            Assert.Equal(
                new[] { "set_this[]", "add_Changed" },
                ResolveAll(owners));
        }

        /// <summary>
        /// Resolves the containing member for every catch in
        /// <paramref name="source"/>, in document order — the same order
        /// Roslyn's DescendantNodes returns them to the auditor.
        /// </summary>
        private static string[] ResolveAll(string source)
        {
            var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();

            return root.DescendantNodes()
                .OfType<CatchClauseSyntax>()
                .Select(cc => SilentCatchAuditTests.FindContainingMethod(root, cc.SpanStart))
                .ToArray();
        }
    }
}
