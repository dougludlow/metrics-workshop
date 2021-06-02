import http from 'k6/http';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export let options = {
	scenarios: {
		constant_request_rate: {
			executor: 'constant-arrival-rate',
			rate: 1,
			timeUnit: '1s',
			duration: '1h',
			preAllocatedVUs: 20,
			maxVUs: 100,
		},
	},
};

export default function () {
	const url = `${BASE_URL}/api/classrooms/2ae08889-59d0-4d2a-920a-083ca2dba1a7/students`;
	http.get(url, { tags: { name: 'ClassroomStudents' } });
}
