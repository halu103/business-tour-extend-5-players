<script lang="ts">
	import { ArrowRight01Icon, CodeIcon, Download04Icon } from '@hugeicons/core-free-icons';
	import { HugeiconsIcon } from '@hugeicons/svelte';
	import * as m from '#lib/paraglide/messages.js';
	import { REPOSITORY_URL } from '#lib';
	import type { Release } from './types.js';

	let { latest }: { latest?: Release } = $props();
	let faqs = $derived([
		{ value: 'smartscreen', question: m.faq_smartscreen_q(), answer: m.faq_smartscreen_a() },
		{ value: 'game-path', question: m.faq_game_path_q(), answer: m.faq_game_path_a() },
		{ value: 'access', question: m.faq_access_q(), answer: m.faq_access_a() },
		{ value: 'four-slots', question: m.faq_four_slots_q(), answer: m.faq_four_slots_a() },
		{ value: 'guests', question: m.faq_guests_q(), answer: m.faq_guests_a() },
		{ value: 'remove', question: m.faq_remove_q(), answer: m.faq_remove_a() }
	]);
</script>

<section id="help" class="section-shell" aria-labelledby="help-title">
	<div class="help-grid">
		<div class="section-heading help-intro">
			<p class="section-kicker">{m.help_kicker()}</p>
			<h2 id="help-title">{m.help_title()}</h2>
			<p>{m.help_description()}</p>
			<a
				href={REPOSITORY_URL}
				target="_blank"
				rel="external noreferrer"
				class="game-button secondary source-button"
				><HugeiconsIcon icon={CodeIcon} strokeWidth={2} aria-hidden="true" />{m.footer_github()}</a
			>
		</div>

		<div class="surface-card faq-list">
			{#each faqs as faq (faq.value)}
				<details class="faq-item">
					<summary>{faq.question}</summary>
					<p class="muted-copy">{faq.answer}</p>
				</details>
			{/each}
		</div>
	</div>

	<aside class="surface-card final-cta" aria-labelledby="footer-cta-title">
		<div class="cta-copy">
			<p class="section-kicker">{m.footer_cta_kicker()}</p>
			<h2 id="footer-cta-title">{m.footer_cta_title()}</h2>
			<p class="muted-copy">{m.footer_cta_body()}</p>
		</div>
		{#if latest?.zipUrl}
			<a href={latest.zipUrl} rel="external" class="game-button"
				><HugeiconsIcon
					icon={Download04Icon}
					strokeWidth={2}
					aria-hidden="true"
				/>{m.hero_download_zip({ version: latest.version || latest.tag })}</a
			>
		{:else}
			<a href="#releases" class="game-button"
				><HugeiconsIcon
					icon={ArrowRight01Icon}
					strokeWidth={2}
					aria-hidden="true"
				/>{m.footer_cta_button()}</a
			>
		{/if}
	</aside>
</section>

<style>
	.help-grid {
		display: grid;
		grid-template-columns: 0.8fr 1.2fr;
		gap: 3rem;
		align-items: start;
	}
	.help-intro {
		margin-bottom: 0;
	}
	.source-button {
		margin-top: 1.4rem;
	}
	.source-button :global(svg) {
		width: 1.15rem;
		height: 1.15rem;
	}
	.faq-list {
		padding: 0.35rem 1.5rem;
	}
	.faq-item + .faq-item {
		border-top: 1px solid var(--border);
	}
	.faq-item summary {
		padding: 1.25rem 0;
		cursor: pointer;
		color: var(--foreground);
		font-size: 1rem;
		line-height: 1.6;
		font-weight: 750;
	}
	.faq-item summary::marker {
		color: var(--primary);
	}
	.faq-item summary:hover,
	.faq-item[open] summary {
		color: var(--primary);
	}
	.faq-item p {
		padding: 0 0 1.25rem;
		margin: 0;
		line-height: 1.8;
	}
	.final-cta {
		margin-top: 4rem;
		padding: 2.5rem;
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: 2rem;
		background: color-mix(in srgb, var(--accent) 13%, var(--card));
		border-color: color-mix(in srgb, var(--accent) 55%, var(--border));
	}
	.cta-copy {
		max-width: 40rem;
	}
	.cta-copy h2 {
		font-size: clamp(1.7rem, 3vw, 2.3rem);
		margin: 0.5rem 0 0.7rem;
		line-height: 1.2;
	}
	.cta-copy > p:last-child {
		margin: 0;
		line-height: 1.7;
	}
	.final-cta .game-button {
		flex: none;
	}
	.final-cta :global(svg) {
		width: 1.2rem;
		height: 1.2rem;
	}
	@media (max-width: 900px) {
		.help-grid {
			grid-template-columns: 1fr;
			gap: 2rem;
		}
		.final-cta {
			align-items: flex-start;
			flex-direction: column;
			padding: 1.7rem;
		}
	}
	@media (max-width: 500px) {
		.faq-list {
			padding-inline: 1.1rem;
		}
		.final-cta .game-button {
			width: 100%;
		}
	}
</style>
