local total = 0

for i = 1, 10 do
	if i == 3 then
		total = total + 30
	elseif i % 2 == 0 then
		total = total + i
	else
		total = total + 1
	end
end

local j = 0
while j < 20 do
	j = j + 1
	if j == 4 then
		break
	end
	total = total + j
end

local function classify(value)
	if value < 0 then
		return "negative"
	elseif value == 0 then
		return "zero"
	else
		return "positive"
	end
end

assert(total == 70, "jump/loop total mismatch: " .. total)
assert(j == 4, "break target mismatch: " .. j)
assert(classify(-1) == "negative")
assert(classify(0) == "zero")
assert(classify(1) == "positive")
print("JUMP_SMOKE_OK", total, j)
