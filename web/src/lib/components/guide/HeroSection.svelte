<script lang="ts">
	import * as m from '#lib/paraglide/messages.js';
	import { REPOSITORY_URL } from '#lib';
	import BoardIllustration from './BoardIllustration.svelte';
	import type { Release } from './types';
	let { latest }: { latest?: Release } = $props();
</script>

<section class="hero-field">
	<div class="sky-shapes" aria-hidden="true"><i></i><i></i><i></i><i></i></div>
	<div class="hero-inner">
		<div class="hero-copy">
			<span class="mini-badge"
				><span class="badge-dot" aria-hidden="true"></span>{m.hero_badge()}</span
			>
			<p class="section-kicker">{m.hero_kicker()}</p>
			<h1>{m.hero_title_line_1()}<span>{m.hero_title_line_2()}</span></h1>
			<p class="hero-description">{m.hero_description()}</p>
			<div class="hero-actions">
				<a href={latest?.zipUrl || '#releases'} class="game-button"
					><span aria-hidden="true">↓</span>{latest?.zipUrl
						? m.hero_download_zip({ version: latest.version || latest.tag })
						: m.hero_no_download()}</a
				>
				<a href="#features" class="explore-link"
					>{m.nav_features()} <span aria-hidden="true">↗</span></a
				>
			</div>
			{#if latest}<p class="latest-note">
					<span aria-hidden="true">●</span>{m.hero_latest({
						version: latest.version || latest.tag
					})}
				</p>{/if}
			<p class="hero-note">{m.hero_note()}</p>
			<a href={REPOSITORY_URL} target="_blank" rel="external noreferrer" class="source-link"
				>{m.hero_source()} <span aria-hidden="true">↗</span></a
			>
		</div>
		<div class="hero-board"><BoardIllustration /></div>
	</div>
	<div class="compatibility-strip">
		<div><span>{m.stat_game_label()}</span><strong>{m.stat_game_value()}</strong></div>
		<div><span>{m.stat_platform_label()}</span><strong>{m.stat_platform_value()}</strong></div>
		<div><span>{m.stat_capacity_label()}</span><strong>{m.stat_capacity_value()}</strong></div>
	</div>
</section>

<style>
	.hero-field {
		position: relative;
		overflow: hidden;
		background: linear-gradient(160deg, #a5c1d2 0%, #89b3cb 48%, #aac6c5 100%);
		border-bottom: 1px solid #8aabbf;
	}
	.hero-inner {
		max-width: 1240px;
		padding: 3.2rem 2.3rem 2.5rem;
		margin: auto;
		display: grid;
		grid-template-columns: 0.94fr 1.06fr;
		gap: 1.5rem;
		align-items: center;
		position: relative;
	}
	.hero-copy {
		padding-bottom: 1.5rem;
		position: relative;
		z-index: 1;
	}
	.hero-copy .mini-badge {
		background: #dce6ece0;
		border-color: #bed1dd;
		box-shadow: 0 2px 0 #8baebf;
	}
	.badge-dot {
		background: #76a43b;
		border-radius: 50%;
		width: 6px;
		height: 6px;
	}
	.hero-copy .section-kicker {
		margin: 2rem 0 1rem;
		font-size: 0.67rem;
		letter-spacing: 0.15em;
	}
	h1 {
		margin: 0;
		font-size: clamp(3.3rem, 5.6vw, 5.2rem);
		line-height: 0.99;
		font-weight: 950;
		letter-spacing: -0.055em;
		color: #164a64;
		text-wrap: balance;
	}
	h1 span {
		display: block;
		color: #006b94;
		margin-top: 0.15em;
	}
	.hero-description {
		margin: 1.8rem 0;
		max-width: 440px;
		font-size: 1.12rem;
		line-height: 1.8;
		color: #24485c;
	}
	.hero-actions {
		display: flex;
		align-items: center;
		flex-wrap: wrap;
		gap: 1.4rem;
	}
	.hero-actions .game-button {
		min-height: 54px;
		font-size: 1rem;
		padding-inline: 1.5rem;
	}
	.explore-link {
		color: #176381;
		font-size: 0.95rem;
		font-weight: 850;
	}
	.latest-note {
		display: flex;
		gap: 0.5rem;
		margin: 1.3rem 0 0.6rem;
		color: #24485c;
		font-size: 0.75rem;
		font-weight: 750;
	}
	.latest-note span {
		color: #649324;
	}
	.hero-note {
		margin-top: 1.2rem;
		max-width: 390px;
		color: #24485c;
		font-size: 0.75rem;
		line-height: 1.8;
	}
	.source-link {
		display: inline-block;
		margin-top: 0.6rem;
		color: #174963;
		font-size: 0.75rem;
		font-weight: 750;
		text-decoration: underline;
		text-underline-offset: 0.2em;
	}
	.hero-board {
		min-width: 0;
		position: relative;
		padding: 1rem 0;
	}
	.compatibility-strip {
		max-width: 1120px;
		margin: 0 auto;
		display: grid;
		grid-template-columns: repeat(3, 1fr);
		padding: 1.4rem 0 1.7rem;
		border-top: 1px solid #91b9ce55;
	}
	.compatibility-strip div {
		padding: 0.2rem 2rem;
	}
	.compatibility-strip div + div {
		border-left: 1px solid #91b9ce55;
	}
	.compatibility-strip span {
		display: block;
		font-size: 0.65rem;
		text-transform: uppercase;
		letter-spacing: 0.1em;
		color: #24485c;
		font-weight: 750;
	}
	.compatibility-strip strong {
		display: block;
		margin-top: 0.45rem;
		font-weight: 850;
		font-size: 0.9rem;
		color: #28536a;
	}
	.sky-shapes {
		position: absolute;
		inset: 0;
		pointer-events: none;
	}
	.sky-shapes i {
		position: absolute;
		width: 65px;
		height: 65px;
		border: 2px solid #ffffff60;
		border-radius: 12px;
		transform: rotate(20deg);
	}
	.sky-shapes i:nth-child(1) {
		left: 3%;
		top: 10%;
	}
	.sky-shapes i:nth-child(2) {
		left: 43%;
		bottom: 10%;
		width: 90px;
		height: 90px;
	}
	.sky-shapes i:nth-child(3) {
		right: 2%;
		top: 25%;
		transform: rotate(-15deg);
	}
	.sky-shapes i:nth-child(4) {
		left: 58%;
		top: 5%;
		width: 40px;
		height: 40px;
	}
	@media (max-width: 900px) {
		.hero-inner {
			grid-template-columns: 1fr;
			max-width: 650px;
			padding: 3.2rem 1.5rem 2rem;
			gap: 1.8rem;
		}
		.hero-copy {
			padding-bottom: 0;
		}
		h1 {
			max-width: 580px;
			font-size: clamp(3.2rem, 9vw, 4.8rem);
		}
		.hero-description {
			max-width: 550px;
		}
		.hero-board {
			max-width: 520px;
			width: 100%;
			margin: auto;
		}
		.compatibility-strip {
			margin-inline: 1rem;
		}
		.compatibility-strip div {
			padding: 0.2rem 1rem;
		}
	}
	@media (max-width: 500px) {
		.hero-inner {
			padding-inline: 1.25rem;
		}
		.compatibility-strip {
			grid-template-columns: 1fr;
			gap: 0.8rem;
		}
		.compatibility-strip div + div {
			border-left: 0;
		}
		.compatibility-strip div {
			display: flex;
			align-items: center;
			justify-content: space-between;
		}
		.compatibility-strip strong {
			margin: 0;
			font-size: 0.8rem;
		}
	}
</style>
