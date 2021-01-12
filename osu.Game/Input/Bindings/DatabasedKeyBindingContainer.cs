// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Input.Bindings;
using osu.Game.Database;
using osu.Game.Rulesets;

namespace osu.Game.Input.Bindings
{
    /// <summary>
    /// A KeyBindingInputManager with a database backing for custom overrides.
    /// </summary>
    /// <typeparam name="T">The type of the custom action.</typeparam>
    public class DatabasedKeyBindingContainer<T> : KeyBindingContainer<T>
        where T : struct
    {
        private readonly RulesetInfo ruleset;

        private readonly int? variant;

        [Resolved]
        private RealmKeyBindingStore store { get; set; }

        [Resolved]
        private RealmContextFactory realmFactory { get; set; }

        public override IEnumerable<IKeyBinding> DefaultKeyBindings => ruleset.CreateInstance().GetDefaultKeyBindings(variant ?? 0);

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="ruleset">A reference to identify the current <see cref="Ruleset"/>. Used to lookup mappings. Null for global mappings.</param>
        /// <param name="variant">An optional variant for the specified <see cref="Ruleset"/>. Used when a ruleset has more than one possible keyboard layouts.</param>
        /// <param name="simultaneousMode">Specify how to deal with multiple matches of <see cref="KeyCombination"/>s and <typeparamref name="T"/>s.</param>
        /// <param name="matchingMode">Specify how to deal with exact <see cref="KeyCombination"/> matches.</param>
        public DatabasedKeyBindingContainer(RulesetInfo ruleset = null, int? variant = null, SimultaneousBindingMode simultaneousMode = SimultaneousBindingMode.None, KeyCombinationMatchingMode matchingMode = KeyCombinationMatchingMode.Any)
            : base(simultaneousMode, matchingMode)
        {
            this.ruleset = ruleset;
            this.variant = variant;

            if (ruleset != null && variant == null)
                throw new InvalidOperationException($"{nameof(variant)} can not be null when a non-null {nameof(ruleset)} is provided.");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            store.KeyBindingChanged += ReloadMappings;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (store != null)
                store.KeyBindingChanged -= ReloadMappings;
        }

        protected override void ReloadMappings()
        {
            if (ruleset != null && !ruleset.ID.HasValue)
                // if the provided ruleset is not stored to the database, we have no way to retrieve custom bindings.
                // fallback to defaults instead.
                KeyBindings = DefaultKeyBindings;
            else
            {
                var rulesetId = ruleset?.ID;

                // #1
                KeyBindings = store.Query(rulesetId, variant).Detach();

                // #2 (Clearly shows lifetime of realm context access)
                using (var realm = realmFactory.Get())
                    KeyBindings = realm.All<RealmKeyBinding>().Where(b => b.RulesetID == rulesetId && b.Variant == variant).Detach();
            }
        }
    }
}
