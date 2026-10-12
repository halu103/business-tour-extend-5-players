<script lang="ts">
	import {
		BookOpen02Icon,
		CheckmarkCircle02Icon,
		PackageCheckIcon,
		UserGroup02Icon
	} from '@hugeicons/core-free-icons';
	import { HugeiconsIcon } from '@hugeicons/svelte';
	import * as m from '#lib/paraglide/messages.js';

	const playerSlots = [2, 3, 4, 5];
	let successItems = $derived([m.verify_success_1(), m.verify_success_2(), m.verify_success_3()]);
</script>

<section id="verify" class="verify-band" aria-labelledby="verify-title">
	<div class="section-shell verify-grid">
		<div>
			<div class="section-heading">
				<p class="section-kicker">{m.verify_kicker()}</p>
				<h2 id="verify-title">{m.verify_title()}</h2>
				<p>{m.verify_description()}</p>
			</div>

			<figure class="surface-card lobby-preview">
				<div class="lobby-slots">
					<div class="lobby-slot host-slot">
						<span class="player-token" aria-hidden="true"
							><HugeiconsIcon icon={UserGroup02Icon} strokeWidth={2} /></span
						><span>{m.slot_host()}</span>
					</div>
					{#each playerSlots as slot (slot)}
						<div class="lobby-slot">
							<span class="player-token" aria-hidden="true"
								><HugeiconsIcon icon={UserGroup02Icon} strokeWidth={2} /></span
							><span>{m.slot_invite({ index: slot })}</span>
						</div>
					{/each}
				</div>
				<figcaption class="muted-copy">{m.verify_preview_note()}</figcaption>
			</figure>
		</div>

		<div class="verify-checks">
			<article class="surface-card success-card">
				<div class="verify-card-heading">
					<HugeiconsIcon icon={PackageCheckIcon} strokeWidth={2} aria-hidden="true" />
					<h3>{m.verify_success_title()}</h3>
				</div>
				<ul class="checklist">
					{#each successItems as item (item)}
						<li>
							<HugeiconsIcon icon={CheckmarkCircle02Icon} strokeWidth={2} aria-hidden="true" /><span
								>{item}</span
							>
						</li>
					{/each}
				</ul>
			</article>
			<aside class="info-notice verify-limit" aria-labelledby="verify-limit-title">
				<HugeiconsIcon icon={BookOpen02Icon} strokeWidth={2} aria-hidden="true" />
				<div>
					<h3 id="verify-limit-title">{m.verify_limit_title()}</h3>
					<p>{m.verify_limit_body()}</p>
				</div>
			</aside>
		</div>
	</div>
</section>

<style>
	.verify-band {
		border-block: 1px solid var(--border);
		background: color-mix(in srgb, var(--primary) 5%, var(--background));
	}
	.verify-grid {
		display: grid;
		grid-template-columns: 1.05fr 0.95fr;
		align-items: center;
		gap: 3rem;
	}
	.lobby-preview {
		margin: 0;
		padding: 1.25rem;
	}
	.lobby-slots {
		display: grid;
		grid-template-columns: repeat(5, minmax(0, 1fr));
		gap: 0.55rem;
	}
	.lobby-slot {
		display: flex;
		flex-direction: column;
		align-items: center;
		gap: 0.7rem;
		padding: 1rem 0.2rem;
		color: var(--foreground);
		text-align: center;
		font-size: 0.73rem;
		font-weight: 750;
		line-height: 1.4;
		border-radius: 0.75rem;
		background: color-mix(in srgb, var(--primary) 4%, white);
	}
	.player-token {
		display: grid;
		place-items: center;
		width: 2.7rem;
		height: 2.7rem;
		border-radius: 50%;
		background: #caeb7f;
		border: 2px solid #8cb82f;
		box-shadow: 0 3px 0 #8cb82f;
		color: var(--foreground);
	}
	.player-token :global(svg) {
		width: 1.4rem;
		height: 1.4rem;
	}
	.lobby-slot:nth-child(2) .player-token {
		background: #ffe777;
		border-color: #d8ab19;
		box-shadow: 0 3px 0 #d8ab19;
	}
	.lobby-slot:nth-child(3) .player-token {
		background: #9bd3ef;
		border-color: #3996c3;
		box-shadow: 0 3px 0 #3996c3;
	}
	.lobby-slot:nth-child(4) .player-token {
		background: #f4b4d0;
		border-color: #d879a3;
		box-shadow: 0 3px 0 #d879a3;
	}
	.lobby-slot:nth-child(5) .player-token {
		background: #ffae91;
		border-color: var(--coral);
		box-shadow: 0 3px 0 var(--coral);
	}
	.lobby-preview figcaption {
		margin-top: 1rem;
		padding-top: 1rem;
		border-top: 1px solid var(--border);
		font-size: 0.8rem;
		line-height: 1.6;
	}
	.verify-checks {
		display: grid;
		gap: 1.2rem;
	}
	.success-card {
		padding: 1.7rem;
	}
	.verify-card-heading {
		display: flex;
		gap: 0.85rem;
		align-items: center;
		margin-bottom: 1.4rem;
	}
	.verify-card-heading :global(svg) {
		width: 2rem;
		height: 2rem;
		color: var(--primary);
		flex: none;
	}
	.verify-card-heading h3 {
		margin: 0;
		font-size: 1.25rem;
		line-height: 1.4;
	}
	.verify-limit {
		display: flex;
		gap: 0.85rem;
	}
	.verify-limit > :global(svg) {
		width: 1.45rem;
		height: 1.45rem;
		color: var(--primary);
		flex: none;
		margin-top: 0.15rem;
	}
	.verify-limit h3 {
		margin: 0 0 0.5rem;
		font-size: 1rem;
	}
	.verify-limit p {
		margin: 0;
		line-height: 1.7;
	}
	@media (max-width: 900px) {
		.verify-grid {
			grid-template-columns: 1fr;
			gap: 2rem;
		}
	}
	@media (max-width: 420px) {
		.lobby-preview {
			padding: 0.85rem;
		}
		.lobby-slots {
			gap: 0.3rem;
		}
		.player-token {
			width: 2.15rem;
			height: 2.15rem;
		}
		.lobby-slot {
			font-size: 0.65rem;
		}
	}
</style>
