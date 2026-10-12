<script lang="ts">
	import {
		Alert02Icon,
		CheckmarkCircle02Icon,
		ComputerCheckIcon,
		ShieldCheckIcon,
		UserGroup02Icon
	} from '@hugeicons/core-free-icons';
	import { HugeiconsIcon } from '@hugeicons/svelte';
	import * as m from '#lib/paraglide/messages.js';

	let hostPoints = $derived([m.host_point_1(), m.host_point_2(), m.host_point_3()]);
	let guestPoints = $derived([m.guest_point_1(), m.guest_point_2(), m.guest_point_3()]);
	let preflightItems = $derived([
		m.preflight_item_1(),
		m.preflight_item_2(),
		m.preflight_item_3(),
		m.preflight_item_4()
	]);
</script>

<section class="section-shell" aria-labelledby="scope-title">
	<aside class="info-notice scope-notice" aria-labelledby="critical-title">
		<HugeiconsIcon icon={Alert02Icon} strokeWidth={2} aria-hidden="true" />
		<div>
			<h3 id="critical-title">{m.critical_title()}</h3>
			<p>{m.critical_body()}</p>
		</div>
	</aside>

	<div class="section-heading">
		<p class="section-kicker">{m.scope_kicker()}</p>
		<h2 id="scope-title">{m.scope_title()}</h2>
		<p>{m.scope_description()}</p>
	</div>

	<div class="scope-grid">
		<article class="surface-card role-card">
			<div class="role-top">
				<span class="role-icon"
					><HugeiconsIcon icon={ComputerCheckIcon} strokeWidth={2} aria-hidden="true" /></span
				>
				<span class="mini-badge">{m.host_badge()}</span>
			</div>
			<h3>{m.host_title()}</h3>
			<p class="muted-copy role-description">{m.host_description()}</p>
			<ul class="checklist">
				{#each hostPoints as point (point)}
					<li>
						<HugeiconsIcon icon={CheckmarkCircle02Icon} strokeWidth={2} aria-hidden="true" /><span
							>{point}</span
						>
					</li>
				{/each}
			</ul>
		</article>

		<article class="surface-card role-card guest-card">
			<div class="role-top">
				<span class="role-icon"
					><HugeiconsIcon icon={UserGroup02Icon} strokeWidth={2} aria-hidden="true" /></span
				>
				<span class="mini-badge">{m.guest_badge()}</span>
			</div>
			<h3>{m.guest_title()}</h3>
			<p class="muted-copy role-description">{m.guest_description()}</p>
			<ul class="checklist">
				{#each guestPoints as point (point)}
					<li>
						<HugeiconsIcon icon={CheckmarkCircle02Icon} strokeWidth={2} aria-hidden="true" /><span
							>{point}</span
						>
					</li>
				{/each}
			</ul>
		</article>

		<aside class="surface-card preflight-card" aria-labelledby="preflight-title">
			<div class="preflight-heading">
				<HugeiconsIcon icon={ShieldCheckIcon} strokeWidth={2} aria-hidden="true" />
				<h3 id="preflight-title">{m.preflight_title()}</h3>
			</div>
			<ol class="preflight-list">
				{#each preflightItems as item, index (item)}
					<li>
						<span class="preflight-number" aria-hidden="true">{index + 1}</span><span>{item}</span>
					</li>
				{/each}
			</ol>
		</aside>
	</div>
</section>

<style>
	.scope-notice {
		display: flex;
		gap: 1rem;
		margin-bottom: 3.5rem;
	}
	.scope-notice > :global(svg) {
		width: 1.5rem;
		height: 1.5rem;
		flex: none;
		color: var(--coral);
		margin-top: 0.15rem;
	}
	.scope-notice h3 {
		margin: 0 0 0.4rem;
		font-size: 1.05rem;
	}
	.scope-notice p {
		margin: 0;
		line-height: 1.7;
	}
	.scope-grid {
		display: grid;
		grid-template-columns: 1fr 1fr 0.9fr;
		gap: 1.25rem;
	}
	.role-card,
	.preflight-card {
		padding: 1.6rem;
	}
	.role-card {
		border-top: 5px solid var(--primary);
	}
	.guest-card {
		border-top-color: var(--accent);
	}
	.role-top {
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: 1rem;
		margin-bottom: 1.25rem;
	}
	.role-icon {
		display: grid;
		place-items: center;
		width: 3rem;
		height: 3rem;
		border-radius: 1rem;
		background: color-mix(in srgb, var(--primary) 10%, white);
		color: var(--primary);
	}
	.role-icon :global(svg) {
		width: 1.65rem;
		height: 1.65rem;
	}
	.role-card h3 {
		margin: 0;
		font-size: 1.25rem;
		line-height: 1.35;
	}
	.role-description {
		margin: 0.75rem 0 1.4rem;
		line-height: 1.65;
	}
	.preflight-heading {
		display: flex;
		align-items: center;
		gap: 0.7rem;
		margin-bottom: 1.2rem;
	}
	.preflight-heading :global(svg) {
		width: 1.45rem;
		height: 1.45rem;
		color: var(--primary);
		flex: none;
	}
	.preflight-heading h3 {
		margin: 0;
		font-size: 1.1rem;
	}
	.preflight-list {
		list-style: none;
		padding: 0;
		margin: 0;
	}
	.preflight-list li {
		display: flex;
		align-items: flex-start;
		gap: 0.8rem;
		padding: 0.9rem 0;
		border-top: 1px solid var(--border);
		line-height: 1.5;
		color: var(--muted-foreground);
	}
	.preflight-number {
		display: grid;
		place-items: center;
		width: 1.55rem;
		height: 1.55rem;
		border-radius: 50%;
		background: color-mix(in srgb, var(--accent) 18%, white);
		color: var(--foreground);
		font-size: 0.75rem;
		font-weight: 800;
		flex: none;
	}
	@media (max-width: 960px) {
		.scope-grid {
			grid-template-columns: 1fr 1fr;
		}
		.preflight-card {
			grid-column: 1 / -1;
		}
		.preflight-list {
			display: grid;
			grid-template-columns: 1fr 1fr;
			column-gap: 1.5rem;
		}
	}
	@media (max-width: 600px) {
		.scope-grid,
		.preflight-list {
			grid-template-columns: 1fr;
		}
		.preflight-card {
			grid-column: auto;
		}
		.scope-notice {
			margin-bottom: 2.5rem;
		}
	}
</style>
