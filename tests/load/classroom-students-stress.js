import http from 'k6/http';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export let options = {
	discardResponseBodies: true,
	scenarios: {
		stress: {
			executor: 'ramping-vus',
			startVUs: 0,
			gracefulRampDown: '30s',
			stages: [
				{ duration: '2m', target: 500 },
				{ duration: '30s', target: 0 },
			],
		},
	},
};

export default function () {
	const url = `${BASE_URL}/api/classrooms/2ae08889-59d0-4d2a-920a-083ca2dba1a7/students`;
	http.get(url, { tags: { name: 'ClassroomStudents' } });
}
