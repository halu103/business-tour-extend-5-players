<script lang="ts">
	import * as m from '#lib/paraglide/messages.js';
	const features = $derived([
		{ title: m.feature_board_title(), body: m.feature_board_body(), mark: '40', tone: 'blue' },
		{ title: m.feature_regions_title(), body: m.feature_regions_body(), mark: '5', tone: 'green' },
		{ title: m.feature_rules_title(), body: m.feature_rules_body(), mark: '↔', tone: 'gold' },
		{ title: m.feature_hud_title(), body: m.feature_hud_body(), mark: '✓', tone: 'coral' },
		{ title: m.feature_art_title(), body: m.feature_art_body(), mark: 'Aa', tone: 'pink' },
		{
			title: m.feature_connection_title(),
			body: m.feature_connection_body(),
			mark: '∞',
			tone: 'purple'
		}
	]);
	const inventory = $derived([
		{ n: 23, label: m.inventory_city() },
		{ n: 5, label: m.inventory_resort() },
		{ n: 3, label: m.inventory_chance() },
		{ n: 2, label: m.inventory_island() },
		{ n: 2, label: m.inventory_tax() },
		{ n: 2, label: m.inventory_trap() }
	]);
</script>

<section id="features" class="section-shell features-section" aria-labelledby="features-title">
	<div class="section-heading">
		<p class="section-kicker">{m.features_kicker()}</p>
		<h2 id="features-title">{m.features_title()}</h2>
		<p>{m.features_description()}</p>
	</div>
	<div class="feature-grid">
		{#each features as feature (feature.title)}
			<article class={'feature-item ' + feature.tone}>
				<span class="feature-mark" aria-hidden="true">{feature.mark}</span>
				<h3>{feature.title}</h3>
				<p>{feature.body}</p>
			</article>
		{/each}
	</div>
	<div class="inventory-panel">
		<div class="inventory-intro">
			<h3>{m.inventory_title()}</h3>
			<p>{m.inventory_service()}</p>
		</div>
		<dl>
			{#each inventory as item (item.label)}<div>
					<dt>{item.label}</dt>
					<dd>{item.n}</dd>
				</div>{/each}
		</dl>
	</div>
</section>

<style>
	.features-section {
		padding-top: 5rem;
	}
	.feature-grid {
		display: grid;
		grid-template-columns: repeat(3, minmax(0, 1fr));
		column-gap: 2rem;
		row-gap: 2.7rem;
	}
	.feature-item {
		padding: 0.5rem 0 0.5rem 1rem;
		border-left: 3px solid #d8eaf3;
	}
	.feature-mark {
		display: grid;
		place-items: center;
		width: 45px;
		height: 45px;
		border-radius: 12px;
		font-weight: 900;
		color: #007ba8;
		background: #dff1f8;
		font-size: 1.1rem;
	}
	.green .feature-mark {
		color: #537a22;
		background: #e3efce;
	}
	.gold .feature-mark {
		color: #856511;
		background: #fff0bf;
	}
	.coral .feature-mark {
		color: #bf492f;
		background: #ffe3d9;
	}
	.pink .feature-mark {
		color: #b3346f;
		background: #fce0ed;
	}
	.purple .feature-mark {
		color: #7753a6;
		background: #ede4f8;
	}
	h3 {
		font-size: 1.13rem;
		font-weight: 900;
		letter-spacing: -0.025em;
		margin: 1rem 0 0.65rem;
	}
	.feature-item p {
		margin: 0;
		font-size: 0.9rem;
		line-height: 1.8;
		color: var(--muted-foreground);
	}
	.inventory-panel {
		margin-top: 3.3rem;
		background: var(--card);
		border: 1px solid var(--border);
		border-radius: 1.2rem;
		padding: 1.7rem 2rem;
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: 2rem;
	}
	.inventory-intro h3 {
		font-size: 1rem;
		margin: 0 0 0.4rem;
	}
	.inventory-intro p {
		font-size: 0.72rem;
		color: var(--muted-foreground);
		margin: 0;
		line-height: 1.5;
	}
	dl {
		display: flex;
		gap: 1.6rem;
		margin: 0;
	}
	dl div {
		display: flex;
		flex-direction: column-reverse;
		text-align: center;
	}
	dt {
		font-size: 0.65rem;
		font-weight: 700;
		color: var(--muted-foreground);
		white-space: nowrap;
	}
	dd {
		font-size: 1.55rem;
		font-weight: 900;
		margin: 0;
		color: #007ba8;
	}
	@media (max-width: 1000px) {
		.inventory-panel {
			flex-direction: column;
			align-items: flex-start;
		}
		dl {
			width: 100%;
			justify-content: space-between;
			flex-wrap: wrap;
		}
	}
	@media (max-width: 750px) {
		.feature-grid {
			grid-template-columns: repeat(2, minmax(0, 1fr));
			column-gap: 1.3rem;
			row-gap: 1.8rem;
		}
	}
	@media (max-width: 450px) {
		.feature-grid {
			grid-template-columns: 1fr;
		}
		.inventory-panel {
			padding: 1.3rem;
		}
		dl {
			display: grid;
			grid-template-columns: repeat(3, 1fr);
			gap: 1.1rem;
		}
	}
</style>
