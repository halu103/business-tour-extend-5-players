import adapter from '@sveltejs/adapter-vercel';
import { mdsvex } from 'mdsvex';

/** @type {import('@sveltejs/kit').Config} */
const config = {
	extensions: ['.svelte', '.svx', '.md'],
	preprocess: [mdsvex({ extensions: ['.svx', '.md'] })],
	compilerOptions: {
		runes: true,
		experimental: { async: true }
	},
	kit: {
		adapter: adapter(),
		alias: {
			'#lib': 'src/lib'
		}
	}
};

export default config;
