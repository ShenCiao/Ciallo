import * as THREE from 'three';
import { MapControls } from "three/addons/controls/MapControls.js";

// Scene
const scene = new THREE.Scene();

// Camera
let camera = new THREE.OrthographicCamera( 
  window.innerWidth / - 2, window.innerWidth / 2, 
  window.innerHeight / 2, window.innerHeight / - 2, 
  -1, 1000
  );
camera.position.z = 1; 
camera.zoom = 100;
camera.updateProjectionMatrix();

// Renderer
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setClearColor("#233143"); // Set background colour
renderer.setSize(window.innerWidth, window.innerHeight);
document.body.appendChild(renderer.domElement); // Add renderer to HTML as a canvas element

// Make Canvas Responsive
window.addEventListener("resize", () => {
  renderer.setSize(window.innerWidth, window.innerHeight); // Update size
  camera.left = -window.innerWidth/2;
  camera.right= window.innerWidth/2;
  camera.top = window.innerHeight/2;
  camera.bottom = -window.innerHeight/2;
  camera.updateProjectionMatrix();
});

// -------------------------------------------------------------------------------
const triangleGeometry = new THREE.BufferGeometry();
const indices = [
	0, 1, 2,
	2, 3, 0,
];
triangleGeometry.setIndex(indices);

const triangleMaterial = new THREE.RawShaderMaterial( {
  uniforms: {
    time: { value: 1.0 }
  },
  vertexShader: document.getElementById( 'vertexShader' ).textContent,
  fragmentShader: document.getElementById( 'fragmentShader' ).textContent,
  side: THREE.DoubleSide,
  transparent: true,
  glslVersion: THREE.GLSL3
} );

const triangleMesh = new THREE.InstancedMesh( triangleGeometry, triangleMaterial, 2);
const vertexPosition0 = new Float32Array( [
  0.0, 0.0,
  3.0, 0.0,
  0.0, 3.0,
]);
const vertexPosition1 = new Float32Array( [
  3.0, 0.0,
  0.0, 3.0,
  0.0, 0.0,
]);
const vertexRadius0 = new Float32Array( [
  0.5,
  1.0,
  0.5,
]);
const vertexRadius1 = new Float32Array( [
  1.0,
  0.5,
  0.5,
]);

triangleMesh.geometry.setAttribute("position0", new THREE.InstancedBufferAttribute(vertexPosition0, 2));
triangleMesh.geometry.setAttribute("radius0", new THREE.InstancedBufferAttribute(vertexRadius0, 1));
triangleMesh.geometry.setAttribute("position1", new THREE.InstancedBufferAttribute(vertexPosition1, 2));
triangleMesh.geometry.setAttribute("radius1", new THREE.InstancedBufferAttribute(vertexRadius1, 1));
scene.add(triangleMesh);

// Trackball Controls for Camera
const controls = new MapControls(camera, renderer.domElement);
controls.enableRotate = false;
controls.enableDamping = false;
controls.screenSpacePanning = true;
// -------------------------------------------------------------------------------
// Rendering Function
const rendering = function () {
  // Rerender every time the page refreshes (pause when on another tab)
  requestAnimationFrame(rendering);
  controls.update();
  renderer.render(scene, camera);
};

rendering();