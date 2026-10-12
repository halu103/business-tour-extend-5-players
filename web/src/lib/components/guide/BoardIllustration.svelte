<script lang="ts">
	import * as m from '#lib/paraglide/messages.js';
	type Point = [number, number];
	const corners: Point[] = [
		[165, 100],
		[435, 100],
		[535, 380],
		[300, 535],
		[65, 380]
	];
	const center: Point = [300, 285];
	const palette = ['#d6e9a1', '#bde5f0', '#f8cbd8', '#ffdbb3', '#e8d5f4'];
	const blend = (a: Point, b: Point, t: number): Point => [
		a[0] + (b[0] - a[0]) * t,
		a[1] + (b[1] - a[1]) * t
	];
	const points = (value: Point[]) => value.map((p) => p.join(',')).join(' ');
	const cells = corners.flatMap((corner, side) =>
		Array.from({ length: 8 }, (_, index) => {
			const next = corners[(side + 1) % 5],
				a = blend(corner, next, index / 8),
				b = blend(corner, next, (index + 1) / 8);
			const ia = blend(a, center, 0.23),
				ib = blend(b, center, 0.23);
			return {
				id: side * 8 + index,
				side,
				quad: points([a, b, ib, ia]),
				center: blend(blend(a, b, 0.5), blend(ia, ib, 0.5), 0.4)
			};
		})
	);
	const resorts = [4, 14, 18, 25, 38],
		chance = [20, 30, 39],
		traps = [12, 32];
	const players = [
		{ x: 165, y: 82, color: '#8da71d' },
		{ x: 450, y: 82, color: '#e3b708' },
		{ x: 555, y: 386, color: '#079fce' },
		{ x: 300, y: 555, color: '#ee087b' },
		{ x: 45, y: 386, color: '#e95b3e' }
	];
</script>

<figure class="board-figure">
	<div class="board-toolbar">
		<span class="mini-badge">{m.board_preview_label()}</span><span class="board-version"
			>40 / 5</span
		>
	</div>
	<svg class="board-svg" viewBox="0 0 600 590" role="img" aria-labelledby="board-title board-desc">
		<title id="board-title">{m.board_preview_label()}</title>
		<desc id="board-desc">{m.board_preview_note()} {m.stat_capacity_value()}</desc>
		<defs>
			<linearGradient id="grass" x2="0" y2="1"
				><stop stop-color="#c0da74" /><stop offset="1" stop-color="#a5c35d" /></linearGradient
			>
			<linearGradient id="board-bed" x2="0" y2="1"
				><stop stop-color="#62829b" /><stop offset="1" stop-color="#25445c" /></linearGradient
			>
			<filter id="board-shadow" x="-.2" y="-.2" width="1.4" height="1.5"
				><feDropShadow
					dx="0"
					dy="14"
					stdDeviation="12"
					flood-color="#23566d"
					flood-opacity=".14"
				/></filter
			>
		</defs>
		<g aria-hidden="true">
			<ellipse cx="300" cy="465" rx="225" ry="65" fill="#5295b0" opacity=".08" />
			<path
				d="M165 118H435L535 398 300 553 65 398Z"
				fill="url(#board-bed)"
				filter="url(#board-shadow)"
			/>
			<polygon
				points={points(corners)}
				fill="#fff8e9"
				stroke="#fff"
				stroke-width="7"
				stroke-linejoin="round"
			/>
			{#each cells as cell (cell.id)}
				<polygon points={cell.quad} fill={palette[cell.side]} stroke="#fff" stroke-width="1.7" />
			{/each}
			<polygon
				points={points(corners.map((p) => blend(p, center, 0.24)))}
				fill="#829079"
				stroke="#fff9e9"
				stroke-width="3"
			/>
			<polygon
				points={points(corners.map((p) => blend(p, center, 0.285)))}
				fill="url(#grass)"
				stroke="#d7e7b3"
				stroke-width="5"
			/>
			<polygon
				points={points(corners.map((p) => blend(p, center, 0.26)))}
				fill="none"
				stroke="#fff"
				stroke-width="2"
				stroke-dasharray="13 12"
				opacity=".8"
			/>
			{#each cells as cell (cell.id)}
				{#if resorts.includes(cell.id)}
					<g transform={'translate(' + cell.center.join(' ') + ')'}
						><ellipse rx="15" ry="8" fill="#ffeab7" /><path
							d="M0 8 4-14"
							stroke="#a7804b"
							stroke-width="4"
						/><path
							d="M4-15q-13-12-22 0 13-3 19 2Q4-27 17-22 8-18 5-14q15-5 19 7-11-5-20-6Z"
							fill="#65a753"
						/></g
					>
				{:else if chance.includes(cell.id)}
					<g transform={'translate(' + cell.center.join(' ') + ')'}
						><circle r="13" fill="#fff" stroke="#fff" stroke-width="3" /><path
							d="M0 0V-13A13 13 0 0 1 13 0Z"
							fill="#ef6081"
						/><path d="M0 0H13A13 13 0 0 1 0 13Z" fill="#f9cd44" /><path
							d="M0 0V13A13 13 0 0 1-13 0Z"
							fill="#67b5d5"
						/><path d="M0 0H-13A13 13 0 0 1 0-13Z" fill="#99be60" /><circle r="4" fill="#fff" /></g
					>
				{:else if traps.includes(cell.id)}
					<g transform={'translate(' + cell.center.join(' ') + ')'}
						><path d="m-13 0 13-8 13 8-13 9Z" fill="#805534" /><path
							d="m-7 0 7-4 7 4-7 4Z"
							fill="#3d3329"
						/></g
					>
				{:else if cell.id === 0}
					<g transform={'translate(' + cell.center.join(' ') + ')'}
						><path d="M-6 13V-16" stroke="#56636b" stroke-width="3" /><path
							d="M-5-16H15L10-9 15-3H-5Z"
							fill="#f5b632"
						/></g
					>
				{:else if cell.id % 3 === 1}
					<g transform={'translate(' + cell.center.join(' ') + ')'}
						><path
							d="M-10-1 0-9 10-1V10H-10Z"
							fill="#fff"
							stroke="#427d93"
							stroke-width="1.3"
						/><path
							d="m-13-1 13-11 13 11"
							fill="none"
							stroke="#0089b8"
							stroke-width="5"
							stroke-linejoin="round"
						/><path d="M-3 10V3H3V10" fill="#69bcd7" /></g
					>
				{/if}
			{/each}
			<text
				x="300"
				y="265"
				text-anchor="middle"
				fill="#365f33"
				font-family="inherit"
				font-size="31"
				font-weight="950"
				letter-spacing="-1">{m.board_center_title()}</text
			>
			<text
				x="300"
				y="292"
				text-anchor="middle"
				fill="#456b40"
				font-family="inherit"
				font-size="12"
				font-weight="750">{m.board_center_subtitle()}</text
			>
			<g transform="translate(279 330) rotate(-12)"
				><rect
					x="-16"
					y="-16"
					width="32"
					height="32"
					rx="7"
					fill="#fff"
					stroke="#d3e3b4"
					stroke-width="2"
				/>{#each [[-7, -7], [7, -7], [0, 0], [-7, 7], [7, 7]] as dot}<circle
						cx={dot[0]}
						cy={dot[1]}
						r="2.5"
						fill="#24485b"
					/>{/each}</g
			>
			<g transform="translate(324 337) rotate(12)"
				><rect
					x="-16"
					y="-16"
					width="32"
					height="32"
					rx="7"
					fill="#fff"
					stroke="#d3e3b4"
					stroke-width="2"
				/>{#each [[-7, -7], [7, 7], [0, 0]] as dot}<circle
						cx={dot[0]}
						cy={dot[1]}
						r="2.5"
						fill="#24485b"
					/>{/each}</g
			>
			{#each players as player, index}
				<circle cx={player.x} cy={player.y + 3} r="18" fill="#588bad" opacity=".2" />
				<circle
					cx={player.x}
					cy={player.y}
					r="18"
					fill={player.color}
					stroke="#fff"
					stroke-width="4"
				/>
				<text
					x={player.x}
					y={player.y + 5}
					text-anchor="middle"
					font-size="14"
					fill="#fff"
					font-weight="900">{index + 1}</text
				>
			{/each}
		</g>
	</svg>
	<div class="board-stats">
		<div><strong>40</strong><span>{m.board_cell_label()}</span></div>
		<div><strong>5</strong><span>{m.board_player_label()}</span></div>
		<div><strong>5</strong><span>{m.board_resort_label()}</span></div>
	</div>
	<figcaption>{m.board_preview_note()}</figcaption>
</figure>

<style>
	.board-figure {
		margin: 0;
		position: relative;
	}
	.board-toolbar {
		display: flex;
		align-items: center;
		justify-content: space-between;
		padding: 0 1.2rem;
	}
	.board-version {
		font-family: var(--font-mono);
		font-size: 0.75rem;
		color: #24485c;
		font-weight: 700;
	}
	.board-svg {
		display: block;
		width: 100%;
		height: auto;
		overflow: visible;
	}
	.board-stats {
		display: flex;
		justify-content: center;
		gap: 2.5rem;
		margin-top: -0.5rem;
	}
	.board-stats div {
		text-align: center;
	}
	.board-stats strong {
		display: block;
		font-size: 1.65rem;
		font-weight: 900;
		color: #005976;
	}
	.board-stats span {
		display: block;
		font-size: 0.7rem;
		font-weight: 750;
		color: #24485c;
	}
	figcaption {
		margin: 1.2rem auto 0;
		max-width: 340px;
		text-align: center;
		font-size: 0.65rem;
		line-height: 1.65;
		color: #24485c;
	}
</style>
