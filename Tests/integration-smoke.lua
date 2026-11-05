local function pack(...)
	return {n = select("#", ...), ...}
end

local function sumAndValues(base, ...)
	local values = pack(...)
	local total = base
	for index = 1, values.n do
		total = total + values[index]
	end
	return total, values[1], values[2], values.n
end

local total, first, second, count = sumAndValues(10, 2, 3)
assert(total == 15 and first == 2 and second == 3 and count == 2)

local counter = 0
for index = 1, 5 do
	counter = counter + index
end
assert(counter == 15)

local function makeCounter(value)
	return function(step)
		value = value + step
		return value
	end
end

local nextValue = makeCounter(4)
assert(nextValue(3) == 7)
assert(nextValue(2) == 9)

local values = {alpha = 2}
values.beta = 3
assert(values.alpha + values.beta == 5)

print("INTEGRATION_SMOKE_OK", total, counter, nextValue(1))
