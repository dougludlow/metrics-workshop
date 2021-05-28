import http from 'k6/http';

import { sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

export let options = {
	stages: [
		{ duration: '2m', target: 100 },
		{ duration: '2m', target: 200 },
		{ duration: '2m', target: 100 },
	],
};

export default function () {
	http.batch([
		[
			'GET',
			`${BASE_URL}/api/classrooms/2ae08889-59d0-4d2a-920a-083ca2dba1a7/students`,
			null,
			{ tags: { name: 'ClassroomStudents' } },
		],
		[
			'GET',
			`${BASE_URL}/api/classrooms/c083af68-069c-4df6-83f5-5fc138ab7283/students`,
			null,
			{ tags: { name: 'ClassroomStudents' } },
		],
		[
			'GET',
			`${BASE_URL}/api/classrooms/8e1b7e7e-6d93-408f-9a4e-6fe68a4efa47/students`,
			null,
			{ tags: { name: 'ClassroomStudents' } },
		],
	]);

	sleep(1);
}
