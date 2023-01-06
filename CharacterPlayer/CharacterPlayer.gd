extends CharacterBody2D

@export var SPEED = 300

func get_input():
	# Detect up/down/left/right keystate and only move when pressed.
	velocity = Vector2()
	if Input.is_action_pressed('ui_right'):
		velocity.x += 1
	if Input.is_action_pressed('ui_left'):
		velocity.x -= 1
	if Input.is_action_pressed('ui_down'):
		velocity.y += 1
	if Input.is_action_pressed('ui_up'):
		velocity.y -= 1

func _physics_process(_delta):
	get_input()
	velocity = velocity.normalized() * SPEED
	move_and_slide()
