<script lang="ts">
	import {
		ArrowRight01Icon,
		Download04Icon,
		FileZipIcon,
		FolderOpenIcon,
		GameController02Icon,
		InstallingUpdates01Icon
	} from '@hugeicons/core-free-icons';
	import { HugeiconsIcon } from '@hugeicons/svelte';
	import * as m from '#lib/paraglide/messages.js';
	import type { Release } from './types.js';

	let { latest }: { latest?: Release } = $props();
	let installSteps = $derived([
		{
			number: '01',
			icon: Download04Icon,
			title: m.install_step_1_title(),
			body: m.install_step_1_body(),
			tip: m.install_step_1_tip()
		},
		{
			number: '02',
			icon: FolderOpenIcon,
			title: m.install_step_2_title(),
			body: m.install_step_2_body(),
			tip: m.install_step_2_tip()
		},
		{
			number: '03',
			icon: InstallingUpdates01Icon,
			title: m.install_step_3_title(),
			body: m.install_step_3_body(),
			tip: m.install_step_3_tip()
		},
		{
			number: '04',
			icon: GameController02Icon,
			title: m.install_step_4_title(),
			body: m.install_step_4_body(),
			tip: m.install_step_4_tip()
		}
	]);
</script>

<section id="install" class="section-shell" aria-labelledby="install-title">
	<div class="section-heading">
		<p class="section-kicker">{m.install_kicker()}</p>
		<h2 id="install-title">{m.install_title()}</h2>
		<p>{m.install_description()}</p>
	</div>

	<ol class="install-grid">
		{#each installSteps as step (step.number)}
			<li class="surface-card install-step">
				<div class="step-top">
					<span class="step-number" aria-hidden="true">{step.number}</span>
					<HugeiconsIcon icon={step.icon} strokeWidth={1.8} aria-hidden="true" />
				</div>
				<h3>{step.title}</h3>
				<p class="muted-copy">{step.body}</p>
				<div class="step-tip">
					<HugeiconsIcon icon={ArrowRight01Icon} strokeWidth={2} aria-hidden="true" /><span
						>{step.tip}</span
					>
				</div>
			</li>
		{/each}
	</ol>

	{#if latest?.zipUrl}
		<div class="install-download">
			<a href={latest.zipUrl} rel="external" class="game-button"
				><HugeiconsIcon
					icon={FileZipIcon}
					strokeWidth={2}
					aria-hidden="true"
				/>{m.download_latest()}</a
			>
		</div>
	{/if}
</section>

<style>
	.install-grid {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 1.25rem;
		padding: 0;
		margin: 0;
		list-style: none;
	}
	.install-step {
		padding: 1.7rem;
		display: flex;
		flex-direction: column;
	}
	.step-top {
		display: flex;
		justify-content: space-between;
		align-items: center;
		margin-bottom: 1.25rem;
	}
	.step-number {
		display: grid;
		place-items: center;
		width: 3rem;
		height: 3rem;
		border-radius: 1rem;
		background: var(--primary);
		color: white;
		font-size: 1.05rem;
		font-weight: 900;
	}
	.install-step:nth-child(even) .step-number {
		background: color-mix(in srgb, var(--accent) 40%, white);
		color: var(--foreground);
	}
	.step-top > :global(svg) {
		width: 2.2rem;
		height: 2.2rem;
		color: var(--primary);
	}
	.install-step h3 {
		margin: 0;
		font-size: 1.3rem;
		line-height: 1.4;
	}
	.install-step p {
		line-height: 1.7;
		margin: 0.75rem 0 1.3rem;
	}
	.step-tip {
		display: flex;
		gap: 0.55rem;
		margin-top: auto;
		padding-top: 1rem;
		border-top: 1px solid var(--border);
		font-size: 0.85rem;
		line-height: 1.6;
		font-weight: 650;
		color: var(--primary);
		overflow-wrap: anywhere;
	}
	.step-tip :global(svg) {
		width: 1.1rem;
		height: 1.1rem;
		flex: none;
		margin-top: 0.1rem;
	}
	.install-download {
		display: flex;
		justify-content: center;
		margin-top: 2rem;
	}
	.install-download :global(svg) {
		width: 1.3rem;
		height: 1.3rem;
	}
	@media (max-width: 650px) {
		.install-grid {
			grid-template-columns: 1fr;
		}
		.install-step {
			padding: 1.4rem;
		}
	}
</style>
