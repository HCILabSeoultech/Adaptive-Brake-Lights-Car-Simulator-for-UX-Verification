# First let's import
import heartpy as hp
import matplotlib.pyplot as plt

# first let's load the clean PPG signal
data, timer = hp.load_exampledata(1)

plt.figure(figsize=(12,4))
plt.plot(data)
plt.show()


sample_rate = hp.get_samplerate_mstimer(timer)


wd, m = hp.process(data, sample_rate)

# plot
plt.figure(figsize=(12,4))
hp.plotter(wd, m)

# display measures computed
for measure in m.keys():
    print('%s: %f' %(measure, m[measure]))